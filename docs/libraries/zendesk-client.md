---
title: Zendesk API client
description: Kiota-generated, OAuth-authenticated clients for the complete Zendesk Support and Help Center REST APIs — typed request builders, typed errors with Retry-After, and OpenTelemetry tracing.
---

## Overview

`ES.FX.Zendesk` ships two [Kiota](https://learn.microsoft.com/en-us/openapi/kiota/)-generated clients
covering the complete [Zendesk REST API](https://developer.zendesk.com/api-reference/), produced from
Zendesk's official OpenAPI specifications:

- `ES.FX.Zendesk.Support.ZendeskSupportApiClient` — the full Support API surface
  (614 operations across 427 paths).
- `ES.FX.Zendesk.HelpCenter.ZendeskHelpCenterApiClient` — the full Help Center API surface
  (177 operations across 116 paths).

Around the generated code sits a small **curated rim** that the generator cannot provide:

- **DI registration** — `AddZendeskClient()` wires both clients (plus a shared `IRequestAdapter` and the
  attachment fetcher) onto an `IHttpClientFactory`-managed `HttpClient`, keyed-capable for
  multi-tenant setups.
- **OAuth 2.0 `client_credentials`** — tokens are acquired from the tenant's `/oauth/tokens` endpoint,
  cached per instance, refreshed proactively before expiry, and re-acquired once automatically if a
  request comes back `401 Unauthorized`.
- **Typed errors** — an innermost `ZendeskResponseGuardHandler` turns non-retryable failures into a
  typed [`ZendeskApiException`](#error-handling) carrying the status code, a bounded response-body
  prefix, and the `Retry-After` hint.
- **`ZendeskAttachmentContentFetcher`** — authenticated attachment-content downloads
  (a raw-binary concern the generated client cannot express).

The generated code lives under `src/ES.FX.Zendesk/Generated/` and is **never edited by hand** — it is
regenerated from committed spec snapshots by the pipeline documented in
[`src/ES.FX.Zendesk/OpenApi/README.md`](https://github.com/emberstack/ES.FX/blob/main/src/ES.FX.Zendesk/OpenApi/README.md)
(see [Regenerating the clients](#regenerating-the-clients)).

> [!TIP]
> Building on [Ignite](../ignite/index.md)? Use the [Zendesk Spark](../ignite/sparks/zendesk.md)
> instead: `builder.IgniteZendeskClient()` adds configuration binding, startup validation, a live health
> check, and tracing wiring on top of this registration.

## Install

```bash
dotnet add package ES.FX.Zendesk
```

```xml
<PackageReference Include="ES.FX.Zendesk" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add
> `Version="…"`.

## Register the client

`AddZendeskClient` lives in the `Microsoft.Extensions.DependencyInjection` namespace and has two
overloads:

```csharp
public static IHttpClientBuilder AddZendeskClient(
    this IServiceCollection services, string? serviceKey = null);

public static IHttpClientBuilder AddZendeskClient(
    this IServiceCollection services,
    Action<ZendeskClientOptions> configureOptions, string? serviceKey = null);
```

The simplest form configures the options inline:

```csharp
builder.Services.AddZendeskClient(options =>
{
    options.Subdomain = "acme";
    options.OAuth.ClientId = builder.Configuration["Zendesk:ClientId"]!;
    options.OAuth.ClientSecret = builder.Configuration["Zendesk:ClientSecret"]!;
});
```

The parameterless form expects you to configure the (named) `ZendeskClientOptions` yourself — for
example bound from configuration, with startup validation:

```csharp
builder.Services.AddOptions<ZendeskClientOptions>()
    .BindConfiguration("Zendesk")
    .ValidateOnStart();

builder.Services.AddZendeskClient();
```

Options are validated (see [Configuration](#configuration)) either at first use or — with
`ValidateOnStart()` — when the host starts.

### What gets registered

| Service | Lifetime | Notes |
| --- | --- | --- |
| `ZendeskSupportApiClient` | Transient | Keyed when `serviceKey` is set. Transient on purpose: each resolution gets a fresh factory-managed `HttpClient`, so the pooled handler chain rotates normally. |
| `ZendeskHelpCenterApiClient` | Transient | Same instance semantics; shares the adapter/handler chain. |
| `IRequestAdapter` | Transient (keyed) | The Kiota adapter behind both clients — also the [escape hatch](#wire-true-json-and-the-re-serialization-hazard) for raw requests. Authentication happens in the `HttpClient` handler chain, so the adapter itself is anonymous. |
| `ZendeskAttachmentContentFetcher` | Transient (keyed) | Authenticated [attachment-content downloads](#attachment-content-downloads). |
| `IZendeskAccessTokenProvider` | Singleton (keyed) | Owns the OAuth token cache and refresh lock for its instance. |
| Named `HttpClient`s | — | `ES.FX.Zendesk` (or `ES.FX.Zendesk[{key}]`) for the API, plus a `….Token` client without the auth handler for token acquisition. |

`AddZendeskClient` returns the `IHttpClientBuilder` of the underlying named client, so you can chain
further customization (extra handlers, resilience — see [Rate limits](#rate-limits-and-resilience)).

### Consume the clients

The generated clients expose **request builders**: the URL path becomes a property/indexer chain and
the HTTP method becomes a `GetAsync` / `PostAsync` / `PutAsync` / `DeleteAsync` call with typed request
and response models. Inject `ZendeskSupportApiClient` (or `ZendeskHelpCenterApiClient`) and follow the
path:

```csharp
public sealed class SupportOverviewService(ZendeskSupportApiClient zendesk)
{
    public async Task<string> DescribeAsync(long ticketId, CancellationToken cancellationToken)
    {
        // GET /api/v2/tickets/{ticket_id}
        var response = await zendesk.Api.V2.Tickets[ticketId]
            .GetAsync(cancellationToken: cancellationToken);
        var ticket = response?.Ticket
            ?? throw new InvalidOperationException($"Ticket {ticketId} returned no payload.");

        return $"{ticket.Subject} — {ticket.Status}";
    }
}
```

Query parameters are set through the request-configuration delegate:

```csharp
// GET /api/v2/search?query=type:ticket status:open
var results = await zendesk.Api.V2.Search.GetAsync(configuration =>
{
    configuration.QueryParameters.Query = "type:ticket status:open";
    configuration.QueryParameters.SortBy = "updated_at";
}, cancellationToken);
```

Writes take a typed request model (models live in `ES.FX.Zendesk.Support.Models` /
`ES.FX.Zendesk.HelpCenter.Models`):

```csharp
using ES.FX.Zendesk.Support.Models;

// POST /api/v2/tickets
var created = await zendesk.Api.V2.Tickets.PostAsync(new TicketCreateRequest
{
    Ticket = new TicketObject
    {
        Subject = "Printer on fire",
        Comment = new TicketCommentObject { Body = "The printer is on fire." },
        Priority = TicketObject_priority.Urgent
    }
}, cancellationToken: cancellationToken);
```

The Help Center client follows the same shape (its builders carry the `.json` path suffix the live
Help Center API requires, e.g. `ArticlesJson`):

```csharp
public sealed class KnowledgeBaseService(ZendeskHelpCenterApiClient helpCenter)
{
    public async Task<int> CountArticlesAsync(CancellationToken cancellationToken)
    {
        // GET /api/v2/help_center/articles.json
        var response = await helpCenter.Api.V2.Help_center.ArticlesJson
            .GetAsync(cancellationToken: cancellationToken);
        return response?.Articles?.Count ?? 0;
    }
}
```

> [!NOTE]
> The builders mirror Zendesk's URL structure, so the
> [Zendesk API reference](https://developer.zendesk.com/api-reference/) doubles as the client's
> operation index: find the endpoint's path and translate it segment-by-segment
> (`/api/v2/users/me` → `Api.V2.Users.Me`). Each generated method also carries the spec's
> documentation as XML docs.

### Register keyed instances

To talk to more than one Zendesk tenant, register each with a distinct `serviceKey` and resolve the
clients as keyed services:

```csharp
builder.Services.AddZendeskClient(options => { options.Subdomain = "acme-support"; /* … */ }, "support");
builder.Services.AddZendeskClient(options => { options.Subdomain = "acme-sales"; /* … */ }, "sales");
```

```csharp
public sealed class CrossTenantReport(
    [FromKeyedServices("support")] ZendeskSupportApiClient support,
    [FromKeyedServices("sales")] ZendeskSupportApiClient sales)
{
    // …
}
```

Each key gets its own options (the options name is the `serviceKey`), its own named `HttpClient`, and
its own token cache. A `null` key is the default instance, resolvable without a key.

## Wire-true JSON and the re-serialization hazard

The generated **models are lossy on re-serialization** — never round-trip a response through them.
Zendesk's spec marks server-assigned fields (`id`, `created_at`, `url`, counts, `next_page`, …) as
`readOnly`, and Kiota's generated `Serialize()` skips read-only properties entirely, so
deserialize-then-reserialize silently strips the most important fields. Typed models are safe for
**requests** and for **reading** fields in code; they are not a JSON round-trip vehicle. The full
hazard write-up lives in
[`src/ES.FX.Zendesk/OpenApi/README.md`](https://github.com/emberstack/ES.FX/blob/main/src/ES.FX.Zendesk/OpenApi/README.md).

When you need the response **exactly as Zendesk sent it** (to persist, forward, or expose to an
agent — this is how the [Zendesk MCP server](./zendesk-mcp-server.md) works), build the request with
the generated builder but send it through the registered `IRequestAdapter` and read the raw stream:

```csharp
public sealed class RawTicketReader(ZendeskSupportApiClient zendesk, IRequestAdapter adapter)
{
    public async Task<JsonDocument?> GetTicketJsonAsync(long ticketId, CancellationToken cancellationToken)
    {
        var request = zendesk.Api.V2.Tickets[ticketId].ToGetRequestInformation();
        var stream = await adapter.SendPrimitiveAsync<Stream>(request, cancellationToken: cancellationToken);
        if (stream is null) return null;

        await using (stream)
        {
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
    }
}
```

Every builder has `To{Method}RequestInformation(...)` counterparts for exactly this purpose — path
templating, encoding, authentication, and the handler pipeline stay intact; only the typed
deserialization is bypassed.

## Attachment content downloads

Attachment *metadata* is read through the generated client (`GET /api/v2/attachments/{id}`); the
*content* behind an attachment's `content_url` is a raw download the generated client cannot express.
The curated `ZendeskAttachmentContentFetcher` (registered by `AddZendeskClient`) covers it:

```csharp
public sealed class AttachmentReader(ZendeskAttachmentContentFetcher fetcher)
{
    public Task<ZendeskAttachmentContent> FetchAsync(
        string contentUrl, string? contentType, CancellationToken cancellationToken) =>
        fetcher.DownloadAsync(contentUrl, contentType, maxContentBytes: 512 * 1024,
            cancellationToken: cancellationToken);
}
```

> [!IMPORTANT]
> `DownloadAsync` downloads the whole payload by default (Zendesk stores files up to 50 MB). Pass
> `maxContentBytes` to cap the download in bounded contexts — reading stops at the cap and
> `Truncated` is set. Text-like content (per the declared content type) is returned decoded; anything
> else — or an unknown charset — is returned as lossless base64 (`Encoding` says which). Because a
> Zendesk `content_url` may point at an externally hosted file, the tenant's credentials are only sent
> to the configured Zendesk host; external HTTPS hosts are fetched anonymously and external non-HTTPS
> URLs are refused.

Capped downloads can be **resumed**: pass `offset` (a raw-byte offset, applied by skip-reading before
decode — `content_url` downloads don't reliably honor HTTP `Range`) and continue from the previous
result's `offset + ReturnedBytes`. `ZendeskAttachmentContent.ReturnedBytes` is the number of raw payload
bytes the content represents; for capped UTF-8 text it can sit slightly under the cap because the cut is
moved back to a clean character boundary — which is exactly what keeps chained continuations
mojibake-free. A hand-picked `offset > 0` — or any offset into non-UTF-8 text — may start mid-character.

## Configuration

`ZendeskClientOptions` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `Subdomain` | `string` | `""` | The `{subdomain}` in `https://{subdomain}.zendesk.com`. Must be a plain DNS label. |
| `BaseUrl` | `string?` | `null` | Explicit base-address override (sandbox, proxy, test double), **including the `/api/v2/` path** — e.g. `https://sandbox.example.com/api/v2/`. Takes precedence over `Subdomain`; must be absolute `http(s)`. |
| `OAuth` | `ZendeskOAuthOptions` | — | The OAuth `client_credentials` settings below. |

Two derived addresses matter, because the generated request templates carry the full `/api/v2/…` path:

- `GetBaseAddress()` — the named `HttpClient`'s `BaseAddress`:
  `https://{subdomain}.zendesk.com/api/v2/` (or the `BaseUrl` override, trailing slash ensured).
- `GetServiceRootAddress()` — the Kiota adapter's `BaseUrl`: the base address with the trailing
  `api/v2/` segment stripped (`https://{subdomain}.zendesk.com`), while preserving any extra path
  prefix a `BaseUrl` override carries (`https://sandbox.example.com/proxy/api/v2/` →
  `https://sandbox.example.com/proxy/`). `AddZendeskClient` wires both for you.

`ZendeskOAuthOptions` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `ClientId` | `string?` | — | The Zendesk confidential OAuth client's `client_id`. Required. |
| `ClientSecret` | `string?` | — | The client's `client_secret`. Required. |
| `Scope` | `string?` | `"read"` | Space-separated scopes (`read`, `write`, or resource scopes like `tickets:read`). |
| `ExpiresIn` | `int?` | Zendesk default | Requested token lifetime in seconds (Zendesk accepts 300–172800). |
| `ExpiryBuffer` | `TimeSpan` | 60 s | How long before real expiry a cached token is refreshed proactively. |
| `TokenEndpoint` | `Uri?` | derived | Explicit token endpoint override. Defaults to `/oauth/tokens` on the resolved base-address host, so a `BaseUrl` override also redirects token requests. |

Misconfiguration fails fast with a clear message: a `Subdomain` or `BaseUrl` is required, the subdomain
must be a single DNS label (so a typo cannot silently build a URL to a different host), `BaseUrl` must
be an absolute `http(s)` URL, and `ClientId` / `ClientSecret` are mandatory.

> [!WARNING]
> `ClientSecret` is a credential. Keep it out of `appsettings.json` in source control — use user
> secrets, environment variables (e.g. `Zendesk__OAuth__ClientSecret`), or a secret store such as the
> [Azure Key Vault Secrets Spark](../ignite/sparks/azure-keyvault-secrets.md).

## Authentication

The client authenticates exclusively with **OAuth 2.0 `client_credentials`** (Zendesk is sunsetting
legacy API-token authentication, so it is deliberately not supported). Token flow:

- Tokens are requested from `POST {tenant}/oauth/tokens` with a JSON body (a Zendesk quirk — not
  form-encoded) using a dedicated handler-free `HttpClient`, so token acquisition never recurses
  through the auth handler.
- The token is cached per instance and refreshed **proactively** `ExpiryBuffer` before it expires.
  Concurrent refreshes are single-flight: under a thundering herd, exactly one token request is made
  and every waiter reuses its result.
- If Zendesk answers `401` despite a cached token (e.g. the token was revoked early), the request is
  retried **exactly once** with a force-refreshed token. Request content is buffered up front so the
  retry can replay it safely.
- Zendesk's `client_credentials` grant returns no refresh token — a new token is simply requested on
  expiry.

> [!IMPORTANT]
> A `client_credentials` token inherits the permissions of the user who created the OAuth client — there
> is no separate service account. Create the OAuth client under a dedicated least-privilege user, and
> request only the scopes you need (`Scope` defaults to `read`).

## Error handling

Failures surface as one of **two exception types**, split by whether the resilience pipeline retries
the status:

- **Non-retryable failures** (4xx other than `408`/`429`) throw a typed `ZendeskApiException`. The
  innermost `ZendeskResponseGuardHandler` raises it before the Kiota adapter can discard the response
  body, so the actual Zendesk error JSON survives.
- **Retryable statuses** (`408`, `429`, `5xx`) pass through the guard untouched — they belong to the
  resilience handler (see [Rate limits](#rate-limits-and-resilience)). When retries are exhausted,
  they surface as Kiota's `Microsoft.Kiota.Abstractions.ApiException`, with the status code and
  response headers — **including `Retry-After`** — preserved.

Callers that handle the raw `HttpResponseMessage` themselves (for example via a Kiota
`NativeResponseHandler`, which bypasses the adapter's error mapping) can apply the exact same
translation with the public `ZendeskResponseGuard.EnsureSuccessAsync(response, cancellationToken)` —
the logic behind the guard handler.

`ZendeskApiException` members:

| Member | Type | Purpose |
| --- | --- | --- |
| `StatusCode` | `HttpStatusCode` | The HTTP status Zendesk returned. |
| `ResponseBody` | `string?` | A bounded prefix (≤ 2 KiB) of the raw response body — Zendesk's actual error JSON. |
| `RetryAfter` | `TimeSpan?` | The `Retry-After` hint, when present. |

```csharp
try
{
    var response = await zendesk.Api.V2.Tickets[ticketId].GetAsync(cancellationToken: cancellationToken);
}
catch (ZendeskApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
{
    // Unknown ticket id — exception.ResponseBody carries Zendesk's error JSON.
}
catch (Microsoft.Kiota.Abstractions.ApiException exception) when (exception.ResponseStatusCode == 429)
{
    // Rate limited and retries exhausted — Retry-After is in exception.ResponseHeaders.
}
```

Token acquisition and attachment-content downloads run outside the Kiota adapter, so *their* failures
— including retry-exhausted `429`s — always throw `ZendeskApiException` (with `RetryAfter` populated).

### Rate limits and resilience

Zendesk enforces per-minute rate limits and answers `429` with `Retry-After`. The client itself does
not retry — pair it with a resilience handler:

- **Under Ignite** this is automatic: `builder.Ignite()` applies the standard resilience handler
  (which honors `Retry-After`) to every `HttpClient` by default. **Do not add a second one** — the
  response guard is calibrated to let exactly the statuses that handler retries pass through, and
  stacking resilience handlers multiplies retry storms.
- **Standalone**, chain it once on the returned builder
  (requires the `Microsoft.Extensions.Http.Resilience` package):

```csharp
builder.Services
    .AddZendeskClient(options => { /* … */ })
    .AddStandardResilienceHandler();
```

> [!WARNING]
> Retries duplicate writes: Zendesk has no idempotency keys, so a retried `POST` that actually reached
> Zendesk can create a duplicate. Keep that in mind for unattended bulk creates.

## Observability

### Tracing

The package exposes **two** `ActivitySource` names on `ZendeskClientInstrumentation` — subscribe to
both to see the full trace:

| Constant | Value | Emits |
| --- | --- | --- |
| `ActivitySourceName` | `ES.FX.Zendesk` | The client's own source, for spans from the curated rim. |
| `KiotaActivitySourceName` | `Microsoft.Kiota.Http.HttpClientLibrary` | The Kiota request adapter's fixed source (not configurable in the Kiota HTTP library) — one span per generated-client request. |

The [Zendesk Spark](../ignite/sparks/zendesk.md) wires both into OpenTelemetry for you; standalone:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(ZendeskClientInstrumentation.ActivitySourceName)
        .AddSource(ZendeskClientInstrumentation.KiotaActivitySourceName));
```

### Logging

The curated rim logs through the standard `ILogger` pipeline — for example token acquisition at
`Debug`, with the token lifetime. Response bodies are **never** logged — they can contain requester
PII; they remain available on `ZendeskApiException.ResponseBody`.

### Metrics

The client adds no custom meter — .NET's built-in `http.client.*` metrics already cover request
counts and durations for the underlying `HttpClient`.

## Regenerating the clients

The generated code is produced by the pipeline in
[`src/ES.FX.Zendesk/OpenApi/`](https://github.com/emberstack/ES.FX/tree/main/src/ES.FX.Zendesk/OpenApi) and its
[`README.md`](https://github.com/emberstack/ES.FX/blob/main/src/ES.FX.Zendesk/OpenApi/README.md) is the authoritative
reference. In short:

- The generated output is **git-ignored** and **regenerated at build time**: `ES.FX.Zendesk.csproj`
  runs `generate.ps1` before compile, incrementally (only when a committed spec/script/tool-pin input
  changes), and feeds the result to the compiler hidden from Solution Explorer. Only the spec snapshots,
  the pipeline scripts, and `Generated/.editorconfig` are committed. Building requires the `kiota` local
  tool (`dotnet tool restore`) and PowerShell 7 (`pwsh`).
- `src/ES.FX.Zendesk/OpenApi/generate.ps1` can also be run by hand from the **committed spec snapshots**
  (`-Refresh` re-downloads Zendesk's latest specs first) — useful when editing a patch.
- `normalize.ps1` applies **recorded, asserted spec patches** first (Zendesk's published specs contain
  constructs that break Kiota or diverge from live-verified API behavior — e.g. the Help Center
  `.json` path suffix). If Zendesk changes the underlying schema, the script fails loudly instead of
  silently generating a wrong client.
- **Never hand-edit** anything under `src/ES.FX.Zendesk/Generated/` — fixes belong in the pipeline
  (spec patches) or in the curated rim.

## See also

- [Zendesk Spark](../ignite/sparks/zendesk.md) — the Ignite integration: config binding, health check, tracing.
- [Zendesk MCP server](./zendesk-mcp-server.md) — a deployable MCP host exposing these clients as 215 agent tools.
- [Framework libraries](./index.md)
- [Ignite overview](../ignite/index.md)
- [`src/ES.FX.Zendesk/OpenApi/README.md`](https://github.com/emberstack/ES.FX/blob/main/src/ES.FX.Zendesk/OpenApi/README.md) — the generation pipeline, spec patches, and generator hazards.
- [Zendesk API reference](https://developer.zendesk.com/api-reference/)
- [Zendesk OAuth client_credentials grant](https://developer.zendesk.com/documentation/ticketing/working-with-oauth/using-oauth-authentication-with-your-application/)
