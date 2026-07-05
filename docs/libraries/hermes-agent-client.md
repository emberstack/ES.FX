---
title: Hermes Agent API client
description: A typed client for the Nous Research Hermes Agent API server — chat completions, Responses API, asynchronous runs, scheduled jobs, sessions and discovery, with streaming, typed errors and OpenTelemetry tracing.
---

## Overview

`ES.FX.NousResearch.HermesAgent` is a typed client for the
[Nous Research Hermes Agent](https://github.com/NousResearch/hermes-agent)
[API server](https://hermes-agent.nousresearch.com/docs/user-guide/features/api-server), built on
`IHttpClientFactory`. Register it once with `AddHermesAgentClient()` and inject `IHermesAgentClient` — a
resource-grouped surface (`hermes.Chat`, `hermes.Runs`, `hermes.Jobs`, …) that mirrors the server's endpoint
groups: the OpenAI-compatible `/v1` surface plus the `/api` jobs and sessions surfaces.

Under the hood the client:

- Registers a typed `HttpClient` whose base address targets your Hermes Agent server (trailing slash
  normalized so relative paths compose), with `Accept: application/json` and an
  `ES.FX.NousResearch.HermesAgent/{version}` `User-Agent` applied.
- Authenticates with a **static bearer key** — a delegating handler stamps
  `Authorization: Bearer {ApiKey}` on every request (see [Authentication](#authentication)).
- Streams the server's `text/event-stream` endpoints as typed `IAsyncEnumerable<…>` event hierarchies you
  consume with `await foreach` — unknown event types surface as `…UnknownEvent` records instead of
  exceptions, so new server event types never break consumers.
- Turns non-success responses into a typed [`HermesAgentApiException`](#error-handling) carrying the status
  code, a bounded response-body prefix, the parsed error object (both server error envelopes) and the
  `Retry-After` hint.
- Emits a client span per operation on the `ES.FX.NousResearch.HermesAgent` `ActivitySource` (see
  [Observability](#observability)).

The surface spans six areas — chat completions, the Responses API, asynchronous runs, scheduled jobs,
sessions, and server discovery/health — see [API areas](#api-areas).

> [!TIP]
> Building on [Ignite](../ignite/index.md)? Use the
> [Hermes Agent Spark](../ignite/sparks/hermes-agent.md) instead:
> `builder.IgniteHermesAgentClient()` adds configuration binding, startup validation, a live health check,
> and tracing wiring on top of this client.

## Install

```bash
dotnet add package ES.FX.NousResearch.HermesAgent
```

```xml
<PackageReference Include="ES.FX.NousResearch.HermesAgent" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add
> `Version="…"`.

## Register the client

`AddHermesAgentClient` lives in the `Microsoft.Extensions.DependencyInjection` namespace:

```csharp
public static IHttpClientBuilder AddHermesAgentClient(
    this IServiceCollection services,
    string? serviceKey = null, Action<HermesAgentClientOptions>? configureOptions = null);
```

The simplest form configures the options inline:

```csharp
builder.Services.AddHermesAgentClient(configureOptions: options =>
{
    options.BaseUrl = "http://localhost:8642";
    options.ApiKey = builder.Configuration["HermesAgent:ApiKey"];
});
```

Without `configureOptions`, the (named) `HermesAgentClientOptions` are expected to be configured by you —
for example bound from configuration, with startup validation:

```csharp
builder.Services.AddOptions<HermesAgentClientOptions>()
    .BindConfiguration("HermesAgent")
    .ValidateOnStart();

builder.Services.AddHermesAgentClient();
```

Options are validated (see [Configuration](#configuration)) either at first use or — with
`ValidateOnStart()` — when the host starts.

### What gets registered

| Service | Lifetime | Notes |
| --- | --- | --- |
| `IHermesAgentClient` | Transient | Keyed when `serviceKey` is set. Transient on purpose: each resolution gets a fresh factory-managed `HttpClient`, so the pooled handler chain rotates normally. |
| `IValidateOptions<HermesAgentClientOptions>` | Singleton | The fail-fast options validator (added once, shared by every instance). |
| Named `HttpClient` | — | `ES.FX.NousResearch.HermesAgent` (or `ES.FX.NousResearch.HermesAgent[{key}]`), with the base address, default headers and the bearer authentication handler. |

`AddHermesAgentClient` returns the `IHttpClientBuilder` of the underlying named client, so you can chain
further customization (extra handlers, resilience — see
[Retries and resilience](#retries-and-resilience)).

### Consume the client

Inject `IHermesAgentClient` and call the areas:

```csharp
public sealed class ReleaseNotesService(IHermesAgentClient hermes)
{
    public async Task<string?> SummarizeAsync(string diff, CancellationToken cancellationToken)
    {
        var completion = await hermes.Chat.CompleteAsync(new HermesAgentChatCompletionRequest
        {
            Messages =
            [
                HermesAgentChatMessage.FromSystem("You are a concise release-notes assistant."),
                HermesAgentChatMessage.FromUser($"Summarize this diff:\n{diff}")
            ]
        }, cancellationToken: cancellationToken);

        return completion.Choices[0].Message?.Content?.Text;
    }
}
```

### Register keyed instances

To talk to more than one Hermes Agent server, register each with a distinct `serviceKey` and resolve them
as keyed services:

```csharp
builder.Services.AddHermesAgentClient("research", options =>
{
    options.BaseUrl = "http://hermes-research:8642";
    options.ApiKey = builder.Configuration["HermesAgent:Research:ApiKey"];
});

builder.Services.AddHermesAgentClient("support", options =>
{
    options.BaseUrl = "http://hermes-support:8642";
    options.ApiKey = builder.Configuration["HermesAgent:Support:ApiKey"];
});
```

```csharp
public sealed class AgentRouter(
    [FromKeyedServices("research")] IHermesAgentClient research,
    [FromKeyedServices("support")] IHermesAgentClient support)
{
    // …
}
```

Each key gets its own options (the options name is the `serviceKey`) and its own named `HttpClient`. A
`null` key is the default instance, resolvable without a key.

## API areas

`IHermesAgentClient` groups operations by server endpoint group:

| Area | Endpoints | Operations |
| --- | --- | --- |
| `Chat` | `POST /v1/chat/completions` | `CompleteAsync`, `StreamAsync` (SSE) |
| `Responses` | `/v1/responses` | `CreateAsync`, `StreamAsync` (SSE), `GetByIdAsync`, `DeleteAsync` |
| `Runs` | `/v1/runs` | `CreateAsync` (202), `GetByIdAsync` (poll), `StreamEventsAsync` (SSE), `StopAsync`, `ResolveApprovalAsync` |
| `Jobs` | `/api/jobs` | `ListAsync`, `CreateAsync`, `GetByIdAsync`, `UpdateAsync` (PATCH), `DeleteAsync`, `PauseAsync`, `ResumeAsync`, `TriggerAsync` |
| `Sessions` | `/api/sessions` | `ListAsync`, `CreateAsync`, `GetByIdAsync`, `UpdateAsync`, `DeleteAsync`, `GetMessagesAsync`, `ForkAsync`, `ChatAsync`, `StreamChatAsync` (SSE) |
| `Server` | `/v1/models`, `/v1/capabilities`, `/v1/skills`, `/v1/toolsets`, `/v1/health`, `/health/detailed` | `GetModelsAsync`, `GetCapabilitiesAsync`, `GetSkillsAsync`, `GetToolsetsAsync`, `GetHealthAsync`, `GetDetailedHealthAsync` |

A few conventions apply across the whole surface:

- **DTOs** are immutable sealed `record` types under `ES.FX.NousResearch.HermesAgent.Abstractions.Models`,
  with nullable members matching what the server actually returns. Write models omit unset (`null`)
  properties, so a `PATCH` sends only the fields you set.
- **Well-known values** ship as constants classes in `ES.FX.NousResearch.HermesAgent.Abstractions` —
  `HermesAgentRunStatuses`, `HermesAgentResponseStatuses`, `HermesAgentJobStates`,
  `HermesAgentScheduleKinds`, `HermesAgentJobLastRunStatuses`, `HermesAgentDeliverModes`,
  `HermesAgentChatFinishReasons`. They are deliberately constants rather than enums: the server's
  vocabularies grow server-side, and string-typed members keep deserialization resilient to values this
  client has not seen yet.
- **Streaming** methods return `IAsyncEnumerable<…>` over an abstract event record with sealed derived
  records per known event type plus an `…UnknownEvent(EventType, Data)` fallback. Nothing is sent until you
  start enumerating; cancelling the enumeration cancels the request.
- **Per-call headers**: the conversational operations (chat complete/stream, responses create/stream, run
  create, session chat/stream-chat) accept an optional `HermesAgentRequestHeaders` record —
  `SessionId` (`X-Hermes-Session-Id`), `SessionKey` (`X-Hermes-Session-Key`, long-term memory scoping) and
  `IdempotencyKey` (`Idempotency-Key`). `SessionId` session continuity is honored **only by the
  chat-completions endpoint** — elsewhere the server merely echoes the effective session id on the response
  header; a run's session is set via `HermesAgentRunRequest.SessionId`, and session chat targets the
  session in the URL. Idempotency keys are honored only by the **non-streaming** chat and responses paths
  (300 s in-memory window); runs do not support them. Header values are validated when the request is
  built — a value containing CR/LF or other characters illegal in an HTTP header throws `FormatException`
  before anything is sent.

### Chat completions

`Chat` targets the OpenAI-compatible `POST /v1/chat/completions`. Only the fields the server acts on are
modeled — other OpenAI fields (`temperature`, `tools`, …) are silently ignored server-side, and
client-supplied tools are never called. `stream` is controlled by the method you call, not by the request.

```csharp
var completion = await hermes.Chat.CompleteAsync(new HermesAgentChatCompletionRequest
{
    Messages = [HermesAgentChatMessage.FromUser("Check the weather in Bucharest and suggest an outfit.")]
}, cancellationToken: cancellationToken);

Console.WriteLine(completion.Choices[0].Message?.Content?.Text);
```

Message content is a string **or** multimodal parts (`HermesAgentMessageContent` — a `string` converts
implicitly; use `HermesAgentMessageContent.FromParts(...)` for text + `image_url` parts).

Streaming yields OpenAI-style chunks interleaved with named `hermes.tool.progress` events:

```csharp
await foreach (var streamEvent in hermes.Chat.StreamAsync(request, cancellationToken: cancellationToken))
{
    switch (streamEvent)
    {
        case HermesAgentChatCompletionChunkEvent chunkEvent:
            Console.Write(chunkEvent.Chunk.Choices.FirstOrDefault()?.Delta?.Content);
            break;

        case HermesAgentToolProgressEvent toolEvent:
            Console.WriteLine($"[{toolEvent.Progress.Status}] {toolEvent.Progress.Tool}: {toolEvent.Progress.Label}");
            break;

            // HermesAgentChatStreamUnknownEvent: forward-compatible fallback — safe to ignore.
    }
}
```

The final chunk carries the finish reason, usage, and — when the run degraded — an `error` and the `hermes`
extension status object. A degraded-but-usable non-streaming run still returns `200` with
`completion.Hermes` set and a finish reason of `length` or `error`; a run with no usable text fails as
`502` (error code `agent_incomplete`).

Every chat completion has an **effective Hermes session id** — derived server-side when the request sent
none, and possibly *rotated* (differing from the request) after session compression. The server reports it
only on the `X-Hermes-Session-Id` response header; the client surfaces it as
`completion.EffectiveSessionId` on `CompleteAsync`, and as a client-synthesized
`HermesAgentChatStreamStartEvent` yielded before the first chunk on `StreamAsync`. Send it as
`HermesAgentRequestHeaders.SessionId` on the next turn to continue the conversation server-side.

### Responses API

`Responses` targets `/v1/responses`, including retrieval and chaining of stored responses:

```csharp
var response = await hermes.Responses.CreateAsync(new HermesAgentResponseRequest
{
    Input = "What are the open action items from yesterday's incident review?",
    Instructions = "Answer as a terse bullet list."
}, cancellationToken: cancellationToken);

var text = response.Output
    .Where(item => item.Type == "message")
    .SelectMany(item => item.Content ?? [])
    .FirstOrDefault(part => part.Type == "output_text")?.Text;

// Chain a follow-up onto the stored response:
var followUp = await hermes.Responses.CreateAsync(new HermesAgentResponseRequest
{
    Input = "Which of those can we automate?",
    PreviousResponseId = response.Id
}, cancellationToken: cancellationToken);
```

`Input` is a plain string (implicit conversion) or a list of input messages with `input_text` /
`input_image` parts; `Conversation` names a server-tracked conversation as an alternative to explicit
`PreviousResponseId` chaining (the two are mutually exclusive). `CreateAsync` also surfaces the effective
Hermes session id the server reports on the `X-Hermes-Session-Id` response header as
`response.EffectiveSessionId` (on the streaming path a client-synthesized
`HermesAgentResponseStreamStartEvent` carrying it is yielded before the first server event).

Streaming surfaces the typed response lifecycle events:

```csharp
await foreach (var streamEvent in hermes.Responses.StreamAsync(request, cancellationToken: cancellationToken))
{
    switch (streamEvent)
    {
        case HermesAgentResponseOutputTextDeltaEvent delta:
            Console.Write(delta.Delta);
            break;

        case HermesAgentResponseCompletedEvent completed:
            Console.WriteLine($"\nDone: {completed.Response?.Usage?.TotalTokens} tokens.");
            break;

        case HermesAgentResponseFailedEvent failed:
            Console.WriteLine($"\nFailed: {failed.Response?.Error?.Message}");
            break;
    }
}
```

> [!IMPORTANT]
> **Non-streaming creates always report `status: "completed"`** — an agent-side failure is placed inside
> the final message text instead of the envelope. If you need a machine-checkable failure signal, use the
> streaming path (terminal `response.failed` event) or inspect stored snapshots. Stored responses live in a
> **100-entry in-memory LRU store**: retrieval refreshes an entry's position, eviction (or a server
> restart) turns `GetByIdAsync` into a `404`. Do not treat the response store as durable storage.

### Runs

`Runs` submits asynchronous agent runs and follows them by polling or by streaming the one-shot event feed:

```csharp
var created = await hermes.Runs.CreateAsync(new HermesAgentRunRequest
{
    Input = "Compare the three most-starred .NET OpenTelemetry exporters and recommend one."
}, cancellationToken: cancellationToken);
// created.Status is the literal acknowledgment "started" (202) — never a run status value.

await foreach (var runEvent in hermes.Runs.StreamEventsAsync(created.RunId!, cancellationToken))
{
    switch (runEvent)
    {
        case HermesAgentRunMessageDeltaEvent delta:
            Console.Write(delta.Delta);
            break;

        case HermesAgentRunApprovalRequestEvent approval:
            // Human-in-the-loop: the run is paused (status waiting_for_approval) until resolved.
            await hermes.Runs.ResolveApprovalAsync(created.RunId!,
                new HermesAgentRunApprovalRequest { Choice = "once" }, cancellationToken);
            break;

        case HermesAgentRunCompletedEvent completed:
            Console.WriteLine($"\nOutput: {completed.Output}");
            break;

        case HermesAgentRunFailedEvent failed:
            Console.WriteLine($"\nFailed: {failed.Error}");
            break;
    }
}
```

Polling is the durable-ish alternative — fields accumulate on the status object as the run progresses:

```csharp
var run = await hermes.Runs.GetByIdAsync(created.RunId!, cancellationToken);
if (run.Status == HermesAgentRunStatuses.Completed) Console.WriteLine(run.Output);
```

> [!WARNING]
> Run state lives in **server memory only** (lost on restart). The event feed is **one-shot**: the
> underlying queue is deleted when the stream ends — there is no replay and no second subscription. An
> event stream that is never consumed is swept after **~300 seconds**, and terminal runs stay pollable for
> **~3600 seconds** after their last update before `GetByIdAsync` starts returning `404`
> (`run_not_found`). Consume the feed promptly or poll — and persist anything you need to keep.

The server enforces a shared concurrency cap on runs and answers `429` with a `Retry-After` header when it
is exceeded (surfaced on [`HermesAgentApiException.RetryAfter`](#error-handling)). `StopAsync` requests a
cooperative stop (the run typically ends `cancelled`); `ResolveApprovalAsync` answers a pending
`approval.request` with `once`, `session`, `always` or `deny` (approval scope is strictly per run).

### Scheduled jobs

`Jobs` manages the server's cron-style scheduled jobs under `/api/jobs`:

```csharp
var job = await hermes.Jobs.CreateAsync(new HermesAgentJobWrite
{
    Name = "Nightly alert digest",
    Schedule = "0 6 * * *",                     // 5-field numeric cron; also "30m", "every 2h", ISO-8601
    Prompt = "Summarize yesterday's alerts and file the digest.",
    Deliver = HermesAgentDeliverModes.Local
}, cancellationToken);

Console.WriteLine($"{job.Id}: next run {job.NextRunAt} ({job.Schedule?.Kind})");
```

The schedule is written as a **string** (relative duration `30m`/`2h`/`1d` for one-shots,
`every {duration}` for intervals, a 5-field numeric cron expression, or an absolute ISO-8601 timestamp) and
comes back parsed as a structured `HermesAgentJobSchedule`. Updates are shallow merges — unset properties
are left untouched:

```csharp
job = await hermes.Jobs.UpdateAsync(job.Id, new HermesAgentJobWrite { Prompt = "New prompt." },
    cancellationToken);

await hermes.Jobs.PauseAsync(job.Id, cancellationToken);   // enabled=false, hidden from ListAsync
await hermes.Jobs.ResumeAsync(job.Id, cancellationToken);  // re-schedules to the next FUTURE occurrence
await hermes.Jobs.TriggerAsync(job.Id, cancellationToken); // re-arms next_run_at=now (fires within ~60 s)
```

> [!WARNING]
> Two confirmed server footguns:
>
> - **An invalid schedule string fails with HTTP `500`**, not `400` — on create *and* on update. Treat a
>   `500` from a job write as "check the schedule syntax first", and validate schedule strings before
>   sending if they come from user input.
> - **Do not set `Repeat` on an update.** On `PATCH` the server stores the bare integer verbatim,
>   replacing the stored `{times, completed}` object with a corrupt shape. Set the repeat budget at create
>   time only.

More jobs behavior worth knowing: `ListAsync` returns only enabled jobs (paused jobs disappear from the
list — fetch them by id), `TriggerAsync` returns the re-armed job, **not** the run result (observe
`LastRunAt`/`LastStatus` afterwards), repeat-limited jobs delete themselves once their budget completes,
`DeleteAsync` also removes the job's local output directory, and the whole surface answers `501` when the
server's cron module is unavailable. Jobs errors use the flat `{"error": "…"}` envelope — see
[Error handling](#error-handling).

### Sessions

`Sessions` covers session CRUD, message history, forking, and session-scoped agent chat. Conversation
history lives server-side — a chat turn sends only the new message:

```csharp
var session = await hermes.Sessions.CreateAsync(cancellationToken: cancellationToken);

var turn = await hermes.Sessions.ChatAsync(session.Id, new HermesAgentSessionChatRequest
{
    Message = "Review the TODO comments in the repo and propose a cleanup plan."
}, cancellationToken: cancellationToken);

Console.WriteLine(turn.Message?.Content);
var effectiveSessionId = turn.SessionId; // may differ from session.Id — use it for follow-ups!
```

The streaming variant emits lifecycle-ordered events, always ending with `done`:

```csharp
await foreach (var chatEvent in hermes.Sessions.StreamChatAsync(effectiveSessionId,
                   new HermesAgentSessionChatRequest { Message = "Now draft the first PR description." },
                   cancellationToken: cancellationToken))
{
    switch (chatEvent)
    {
        case HermesAgentSessionChatAssistantDeltaEvent delta:
            Console.Write(delta.Delta);
            break;

        case HermesAgentSessionChatToolStartedEvent tool:
            Console.WriteLine($"\n[tool] {tool.ToolName}: {tool.Preview}");
            break;

        case HermesAgentSessionChatRunCompletedEvent completed:
            // completed.Messages is the authoritative per-turn transcript (assistant + tool messages).
            break;

        case HermesAgentSessionChatErrorEvent error:
            Console.WriteLine($"\nTurn failed: {error.Message}");
            break;

        case HermesAgentSessionChatDoneEvent:
            break; // always the final event
    }
}
```

Session behaviors worth knowing:

- **The effective session id can rotate.** `ChatAsync` returns it on the result; on the stream it is
  carried by `assistant.completed` / `run.completed`. Always use the returned id for follow-ups.
- **`ForkAsync` is not read-only**: it copies messages into a new session *and ends the source session*
  with `end_reason: "branched"`.
- `DeleteAsync` removes the session **and all its messages** (irreversible); delegate-subagent children are
  cascade-deleted, branch/compression children are orphaned but survive.
- `GetMessagesAsync` may resolve to a *different* session id than requested (compressed sessions resolve to
  the descendant holding the live messages) — follow the id on the result.
- `ListAsync` excludes archived sessions, silently coerces invalid query values to defaults, and its
  `HasMore` is a page-full heuristic, not an exact remaining count.
- `UpdateAsync` can set only `Title` and `EndReason`; the first end reason wins (re-ending is silently
  ignored).

### Server discovery and health

`Server` exposes the discovery and health endpoints:

```csharp
var capabilities = await hermes.Server.GetCapabilitiesAsync(cancellationToken); // authenticated
Console.WriteLine($"{capabilities.Platform} — {capabilities.Model}");

var models = await hermes.Server.GetModelsAsync(cancellationToken);
var skills = await hermes.Server.GetSkillsAsync(cancellationToken);
var toolsets = await hermes.Server.GetToolsetsAsync(cancellationToken);

var health = await hermes.Server.GetHealthAsync(cancellationToken); // liveness — no auth required
```

`GetHealthAsync` (`GET /v1/health`, an alias of `GET /health`) is the **only unauthenticated call** — a
success proves reachability but *not* that your API key is valid. `GetCapabilitiesAsync` requires
authentication, so a successful call also validates the key — that is exactly what the
[Spark's health check](../ignite/sparks/hermes-agent.md) uses.

## Configuration

`HermesAgentClientOptions` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `BaseUrl` | `string?` | `null` | The absolute `http(s)` base URL of the Hermes Agent API server (e.g. `http://localhost:8642`). Required. A missing trailing slash is appended automatically. |
| `ApiKey` | `string?` | `null` | The static API server key sent as `Authorization: Bearer {ApiKey}` on every request. Required. |

Misconfiguration fails fast with a clear message: `BaseUrl` must be present and an absolute `http(s)` URL,
and `ApiKey` is mandatory (the server refuses to start without a configured key, so in practice
authentication is always on).

> [!WARNING]
> `ApiKey` is a credential. Keep it out of `appsettings.json` in source control — use user secrets,
> environment variables (e.g. `HermesAgent__ApiKey` for the standalone binding shown above, or
> `Ignite__NousResearch__HermesAgent__ApiKey` when configured through the
> [Hermes Agent Spark](../ignite/sparks/hermes-agent.md)), or a secret store such as the
> [Azure Key Vault Secrets Spark](../ignite/sparks/azure-keyvault-secrets.md).

## Authentication

The client authenticates with a **static bearer key**: a delegating handler reads the (named) options and
stamps `Authorization: Bearer {ApiKey}` on every request. There is no token exchange, caching or refresh —
rotation is a configuration change (the handler reads through `IOptionsMonitor`, so a reloaded
configuration value is picked up by subsequent requests without a restart).

Server-side, everything except `GET /health` / `GET /v1/health` requires the key. The session headers
(`X-Hermes-Session-Id`, `X-Hermes-Session-Key`) additionally require the *server* to have a key configured
— otherwise the server answers `403`.

## Error handling

Any non-success response throws a `HermesAgentApiException`:

| Member | Type | Purpose |
| --- | --- | --- |
| `StatusCode` | `HttpStatusCode` | The HTTP status the server returned. |
| `ResponseBody` | `string?` | A bounded prefix (≤ 2 KiB) of the raw response body — the server's actual error JSON. |
| `Error` | `HermesAgentError?` | The parsed error object (`Message`, `Type`, `Param`, `Code`), when the body carried one. |
| `RetryAfter` | `TimeSpan?` | The `Retry-After` hint, when present (sent with `429` when the concurrent-run cap is hit). |

The server uses **two error envelopes**, and both are parsed onto `Error`:

- The OpenAI-style envelope on the `/v1` and sessions surfaces —
  `{"error": {"message", "type", "param", "code"}}`.
- The flat jobs envelope — `{"error": "<string>"}` — mapped with only `Error.Message` set.

```csharp
try
{
    var run = await hermes.Runs.GetByIdAsync(runId, cancellationToken);
}
catch (HermesAgentApiException exception) when (exception.Error?.Code == "run_not_found")
{
    // Unknown, expired or swept run — see the retention notes under Runs.
}
catch (HermesAgentApiException exception) when (exception.StatusCode == HttpStatusCode.TooManyRequests)
{
    var wait = exception.RetryAfter ?? TimeSpan.FromSeconds(5);
    // Back off and retry — the concurrent-run cap was hit.
}
```

Operations the server answers with an empty body where a payload is required throw
`InvalidOperationException` with the operation name in the message. On streaming methods, request
validation failures throw before the first event; mid-stream agent failures arrive **in-stream** as typed
error/failed events (see the per-area sections) rather than as exceptions.

### Retries and resilience

The client itself does not retry — pair it with a resilience handler:

- **Under Ignite** this is automatic: `builder.Ignite()` applies the standard resilience handler (which
  honors `Retry-After`) to every `HttpClient` by default.
- **Standalone**, chain it on the returned builder (requires the
  `Microsoft.Extensions.Http.Resilience` package):

```csharp
builder.Services
    .AddHermesAgentClient(configureOptions: options => { /* … */ })
    .AddStandardResilienceHandler();
```

> [!IMPORTANT]
> Only the **non-streaming** chat and responses paths honor an `Idempotency-Key` (300 s in-memory window;
> the request body fingerprint must also match). Every other `POST` — runs, jobs, sessions — has no
> idempotency support, so a retried request that actually reached the server can duplicate work (submit a
> second run, create a second job, …). Keep that in mind before adding aggressive retry policies to write
> paths.

## Server quirks and limits

Consumer-relevant server behaviors, collected in one place (each is also noted on the relevant area above):

| Quirk / limit | Behavior |
| --- | --- |
| Invalid job schedule string | HTTP **`500`** (not 400) on create and update — check schedule syntax before blaming the server |
| `PATCH` with `repeat` | Corrupts the stored `{times, completed}` object — set the repeat budget at create time only |
| Non-streaming responses | Envelope always reports `status: "completed"`; agent errors hide inside the final message text |
| Response store | 100 entries, in-memory LRU — eviction/restart turns `GetByIdAsync` into `404` |
| Run event feed | One-shot, no replay; unconsumed streams swept after ~300 s |
| Run status retention | Terminal runs pollable ~3600 s after last update, then `404`; all run state is in-memory (lost on restart) |
| Request body cap | 10 MB — larger bodies are rejected with `413` (`body_too_large`) |
| Unauthenticated endpoints | Only `GET /health` / `GET /v1/health`; everything else requires the bearer key |
| Chat/session message caps | 65,536 characters per message/content part, 1,000 parts per array (session chat truncates silently) |
| Jobs surface availability | The whole `/api/jobs` surface answers `501` when the server's cron module is unavailable |
| Job trigger latency | `TriggerAsync` re-arms the job; the scheduler fires it on its next tick (within ~60 s) |
| Error-body capture (2 KiB) | **Client-side** — bounds `HermesAgentApiException.ResponseBody` only; diagnostics, never data |

## Observability

### Tracing

Every operation runs inside a client `Activity` on the `ES.FX.NousResearch.HermesAgent` `ActivitySource`
(`HermesAgentClientInstrumentation.ActivitySourceName`), named `HermesAgent.{Area}.{Operation}` (e.g.
`HermesAgent.Chat.Complete`, `HermesAgent.Runs.StreamEvents`), tagged with `hermesagent.operation`, with
status and exception details recorded on failure. For streaming operations the span covers the **entire
stream consumption**, not just the initial request — a long-lived `await foreach` is a long-lived span. The
[Hermes Agent Spark](../ignite/sparks/hermes-agent.md) wires the source into OpenTelemetry for you;
standalone:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(HermesAgentClientInstrumentation.ActivitySourceName));
```

### Logging

The client logs through the standard `ILogger` pipeline: `Debug` on success, `Warning` on failure (with
the operation and, for API errors, the status code), and nothing on caller-initiated cancellation.
Response bodies and the API key are **never** logged — bodies can contain conversation content; the
truncated error body remains available on `HermesAgentApiException.ResponseBody`.

### Metrics

The client adds no custom meter — .NET's built-in `http.client.*` metrics already cover request counts and
durations for the underlying `HttpClient`.

## See also

- [Hermes Agent Spark](../ignite/sparks/hermes-agent.md) — the Ignite integration: config binding, health check, tracing.
- [Framework libraries](./index.md)
- [Ignite overview](../ignite/index.md)
- [Hermes Agent API server guide](https://hermes-agent.nousresearch.com/docs/user-guide/features/api-server)
- [Hermes Agent on GitHub](https://github.com/NousResearch/hermes-agent)
