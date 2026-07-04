---
title: Zendesk API client
description: A typed, OAuth-authenticated client for the Zendesk Support REST API — resource-grouped operations, typed errors with Retry-After, and OpenTelemetry tracing.
---

## Overview

`ES.FX.Zendesk` is a typed client for the [Zendesk Support REST API](https://developer.zendesk.com/api-reference/)
(including Help Center articles), built on `IHttpClientFactory`. Register it once with
`AddZendeskClient()` and inject `IZendeskClient` — a resource-grouped surface
(`zendesk.Tickets`, `zendesk.Users`, …) that mirrors the Zendesk API structure.

Under the hood the client:

- Registers a typed `HttpClient` whose base address targets `https://{subdomain}.zendesk.com/api/v2/`,
  with `Accept: application/json` and an `ES.FX.Zendesk/{version}` `User-Agent` applied.
- Authenticates with **OAuth 2.0 `client_credentials`** — tokens are acquired from the tenant's
  `/oauth/tokens` endpoint, cached per instance, refreshed proactively before expiry, and re-acquired
  once automatically if a request comes back `401 Unauthorized`.
- Turns non-success responses into a typed [`ZendeskApiException`](#error-handling) carrying the status
  code, a bounded response-body prefix, and the `Retry-After` hint.
- Emits a client span per operation on the `ES.FX.Zendesk` `ActivitySource` (see
  [Observability](#observability)).

The surface covers reads **and** writes across seventeen resource areas — see
[Resource areas](#resource-areas) and [Write operations](#write-operations).

> [!TIP]
> Building on [Ignite](../ignite/index.md)? Use the [Zendesk Spark](../ignite/sparks/zendesk.md)
> instead: `builder.IgniteZendeskClient()` adds configuration binding, startup validation, a live health
> check, and tracing wiring on top of this client.

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
| `IZendeskClient` | Transient | Keyed when `serviceKey` is set. Transient on purpose: each resolution gets a fresh factory-managed `HttpClient`, so the pooled handler chain rotates normally. |
| `IZendeskAccessTokenProvider` | Singleton (keyed) | Owns the OAuth token cache and refresh lock for its instance. |
| Named `HttpClient`s | — | `ES.FX.Zendesk` (or `ES.FX.Zendesk[{key}]`) for the API, plus a `….Token` client without the auth handler for token acquisition. |

`AddZendeskClient` returns the `IHttpClientBuilder` of the underlying named client, so you can chain
further customization (extra handlers, resilience — see [Rate limits](#rate-limits-and-resilience)).

### Consume the client

Inject `IZendeskClient` and call the resource areas:

```csharp
public sealed class SupportOverviewService(IZendeskClient zendesk)
{
    public async Task<string> SummarizeAsync(long ticketId, CancellationToken cancellationToken)
    {
        var ticket = await zendesk.Tickets.GetByIdAsync(ticketId, cancellationToken);
        var comments = await zendesk.Tickets.GetCommentsAsync(
            ticketId, perPage: 25, cancellationToken: cancellationToken);

        return $"{ticket.Subject} — {ticket.Status}, {comments.Count} comments";
    }
}
```

### Register keyed instances

To talk to more than one Zendesk tenant, register each with a distinct `serviceKey` and resolve them as
keyed services:

```csharp
builder.Services.AddZendeskClient(options => { options.Subdomain = "acme-support"; /* … */ }, "support");
builder.Services.AddZendeskClient(options => { options.Subdomain = "acme-sales"; /* … */ }, "sales");
```

```csharp
public sealed class CrossTenantReport(
    [FromKeyedServices("support")] IZendeskClient support,
    [FromKeyedServices("sales")] IZendeskClient sales)
{
    // …
}
```

Each key gets its own options (the options name is the `serviceKey`), its own named `HttpClient`, and
its own token cache. A `null` key is the default instance, resolvable without a key.

## Resource areas

`IZendeskClient` groups operations by Zendesk resource:

| Area | Operations |
| --- | --- |
| `Tickets` | Reads: `ListAsync` (cursor), `GetByIdAsync`, `GetManyAsync` (chunked `show_many`), `CountAsync`, `GetByExternalIdAsync`, `SearchAsync` (auto-scoped to `type:ticket`), `GetCommentsAsync` (`bodyFormat`: `plain`/`rich`/`both`), `GetCommentsCountAsync`, `GetCollaboratorsAsync`, `GetAuditsAsync`, `GetMetricsAsync`, `GetIncidentsAsync`, `GetSideConversationsAsync`, `GetMetricEventsAsync` + `GetIncrementalAsync` (incremental exports). Writes: `CreateAsync`, `CreateManyAsync`, `UpdateAsync` (returns ticket + audit), `UpdateManyAsync` (bulk + batch), `DeleteAsync`, `DeleteManyAsync`, `MergeAsync`, `MarkAsSpamAsync` (+many), `RestoreDeletedAsync` (+many), `DeletePermanentlyAsync` (+many), `SetTagsAsync`/`AddTagsAsync`/`RemoveTagsAsync`, `MakeCommentPrivateAsync`, `RedactCommentAttachmentAsync`, `ImportAsync` (+many) |
| `Users` | Reads: `GetCurrentUserAsync`, `ListAsync` (cursor, role filter), `GetByIdAsync`, `GetManyAsync` (chunked), `CountAsync`, `SearchAsync`, `AutocompleteAsync`, `GetRelatedInformationAsync`, `GetIdentitiesAsync`, `GetGroupsAsync`, `GetOrganizationsAsync`, `GetRequestedTicketsAsync`, `GetAssignedTicketsAsync`, `GetCcdTicketsAsync`. Writes: `CreateAsync`, `CreateOrUpdateAsync` (+many variants), `UpdateAsync`, `UpdateManyAsync` (bulk + batch), `MergeAsync`, `DeleteAsync`, `DeleteManyAsync`, `DeletePermanentlyAsync`, identity `Create`/`Update`/`MakePrimary`/`Verify`/`RequestVerification`/`Delete` |
| `Organizations` | Reads: `ListAsync` (cursor), `GetByIdAsync`, `GetManyAsync` (chunked), `CountAsync`, `SearchAsync` (exact name/external id), `AutocompleteAsync`, `GetUsersAsync`, `GetMembershipsAsync`, `GetTicketsAsync`. Writes: `CreateAsync` (+many), `CreateOrUpdateAsync`, `UpdateAsync` (+many bulk/batch), `DeleteAsync` (+many), `MergeAsync` + `GetMergeAsync` (async merge with its own envelope), membership `Create` (+many)/`Delete` (+many)/`MakeDefault` |
| `Groups` | Reads: `ListAsync`, `GetAssignableAsync`, `GetByIdAsync`, `CountAsync`, `GetUsersAsync`, `GetMembershipsAsync`. Writes: `CreateAsync`, `UpdateAsync`, `DeleteAsync`, membership `Create` (+many)/`Delete` (+many)/`MakeDefault` |
| `Search` | `CountAsync` (size a query cheaply), `ExportTicketsAsync` (cursor-based export — no 1,000-result cap) |
| `Views` | Reads: `ListAsync`, `GetByIdAsync`, `GetTicketsAsync`, `GetTicketCountAsync`. Writes: `CreateAsync`/`UpdateAsync` (write shape: `All`/`Any` conditions + `Output`), `DeleteAsync` |
| `Articles` | `SearchAsync` (Help Center full-text; lean results), `GetByIdAsync` (full body), `ListAsync` (cursor; optional section scope), `ListSectionsAsync`, `GetSectionByIdAsync`, `ListCategoriesAsync`, `GetCategoryByIdAsync` |
| `TicketFields` | Reads: `ListAsync` (cursor or unpaginated — this endpoint has no offset paging), `GetByIdAsync`, `GetOptionsAsync`. Writes: `CreateAsync`, `UpdateAsync` (option set replaces wholesale!), `DeleteAsync`, `CreateOrUpdateOptionAsync`, `DeleteOptionAsync` |
| `Macros` | Reads: `ListAsync`, `ListActiveAsync`, `GetByIdAsync`. Writes: `CreateAsync`, `UpdateAsync` (actions replace wholesale!), `DeleteAsync` |
| `Forms` | Reads: `ListAsync` (cursor), `GetByIdAsync`. Writes: `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `CloneAsync` |
| `Brands` | Reads: `ListAsync` (cursor), `GetByIdAsync`. Writes: `CreateAsync`, `UpdateAsync`, `DeleteAsync` |
| `CustomStatuses` | Reads: `ListAsync`, `GetByIdAsync`. Writes: `CreateAsync`, `UpdateAsync`, `DeleteAsync` |
| `JobStatuses` | `ListAsync` (cursor), `GetByIdAsync`, `GetManyAsync` — poll the async jobs returned by bulk writes |
| `Tags` | `ListAsync` (usage counts; offset **and** cursor — use the cursor for the tail past Zendesk's 10k offset cap), `CountAsync`, `AutocompleteAsync`. Per-entity tag reads live on their areas: `Users.GetTagsAsync`, `Organizations.GetTagsAsync`, `ZendeskTicket.Tags`; tag writes via `SetTagsAsync`/`AddTagsAsync`/`RemoveTagsAsync` (tickets) and the `Tags` property on user/organization writes |
| `SuspendedTickets` | `ListAsync` (cursor), `GetByIdAsync`, `RecoverAsync` (+many), `DeleteAsync` (+many) |
| `Uploads` | `UploadAsync` (raw binary; token chaining for multi-file), `DeleteAsync` — tokens attach files to comments |
| `Attachments` | `GetContentAsync` — authenticated content download |

A few behaviors worth knowing:

- **Pagination** comes in two dialects, mirroring Zendesk. Offset (`page`, `perPage`; max 100 per page)
  is supported where Zendesk documents it, but Zendesk hard-caps offset paging at 100 pages / 10,000
  records — beyond that the API returns `400`. **Cursor pagination** (`pageSize`, `afterCursor` →
  `page[size]`/`page[after]`) has no depth limit and is Zendesk's recommended direction; results expose
  `Meta` (`HasMore`, `AfterCursor`) — keep passing `Meta.AfterCursor` while `HasMore` is `true`. Some
  endpoints are cursor-only (job statuses list, brands, search export). The incremental exports page
  their own way: `GetMetricEventsAsync` by `EndTime`→`startTime`, `GetIncrementalAsync` by
  `AfterCursor`→`cursor`, both until `EndOfStream` is `true`.
- **Sideloading (`include`)**: everywhere Zendesk documents it, list/search/batch operations accept an
  `include` list and return the related records inline as sibling arrays — one roundtrip instead of one
  call per referenced id. Ticket surfaces (list, search, show_many, per-user/org/view lists, audits,
  incremental export) sideload `users`/`groups`/`organizations` (+`comment_count` enriching each
  ticket); comments sideload their authors (`users`); user surfaces sideload
  `organizations`/`groups`/`identities`; groups and group memberships sideload `users`; ticket fields
  sideload their creators; Help Center article lists sideload `users`/`sections`/`categories`. Chunked
  batch calls (`GetManyAsync`) merge and de-duplicate sideloads across chunks. Ticket search uses
  Zendesk's nested `tickets(...)` include syntax automatically.
- **DTOs** are immutable `record` types under `ES.FX.Zendesk.Abstractions.Models`, with nullable
  members matching what Zendesk actually returns.
- **Well-known values** ship as constants classes in `ES.FX.Zendesk.Abstractions` —
  `ZendeskTicketStatuses`, `ZendeskTicketPriorities`, `ZendeskTicketTypes`, `ZendeskUserRoles`,
  `ZendeskIdentityTypes`, `ZendeskStatusCategories`, `ZendeskSideloads`, `ZendeskSortOrders`,
  `ZendeskTicketSortFields`, `ZendeskCommentBodyFormats`, `ZendeskOAuthScopes`,
  `ZendeskJobStatusValues`, `ZendeskOrganizationMergeStatuses` — so vocabulary arguments are
  discoverable and typo-proof (`Status = ZendeskTicketStatuses.Solved`). They are deliberately
  constants rather than enums: Zendesk's vocabularies grow server-side, and string-typed members keep
  deserialization resilient to values this client has not seen yet.

> [!IMPORTANT]
> `Attachments.GetContentAsync` downloads the whole payload by default (Zendesk stores files up to
> 50 MB). Pass `maxContentBytes` to cap the download in bounded contexts — reading stops at the cap and
> `Truncated` is set. Text-like content (per the declared content type) is returned decoded; anything
> else — or an unknown charset — is returned as lossless base64 (`Encoding` says which). Because a
> Zendesk `content_url` may point at an externally hosted file, the tenant's credentials are only sent
> to the configured Zendesk host; external HTTPS hosts are fetched anonymously and external non-HTTPS
> URLs are refused.

## Write operations

Writes use the same typed pattern as reads, with `*Write` request records whose unset (`null`)
properties are **omitted** from the request — an update sends only the fields you set:

```csharp
var result = await zendesk.Tickets.UpdateAsync(ticketId, new ZendeskTicketWrite
{
    Status = "solved",
    Comment = new ZendeskTicketCommentWrite { Body = "Fixed — closing.", Public = true }
}, cancellationToken);
// result.Ticket is the updated ticket; result.Audit describes the change.
```

Things to know before writing:

- **OAuth scope**: the configured `Scope` must include `write` (the default is `read`).
- **Async jobs**: bulk operations (`CreateManyAsync`, `UpdateManyAsync`, `DeleteManyAsync`, merges,
  spam-marking, permanent deletes) return a `ZendeskJobStatus` — poll it via `JobStatuses.GetByIdAsync`
  until `Status` is `completed`/`failed`. Bulk requests accept 1–100 items (validated client-side).
- **Optimistic locking**: since May 2025 Zendesk answers concurrent ticket updates with
  `409 Conflict`. Set `SafeUpdate = true` + `UpdatedStamp` (the ticket's latest `updated_at`) on
  `ZendeskTicketWrite` for explicit collision protection, and be prepared to catch a `409`
  `ZendeskApiException` and retry with fresh data.
- **Destructive array semantics**: `TicketFields.UpdateAsync` (`CustomFieldOptions`),
  `Macros.UpdateAsync` (`Actions`), and `Views.UpdateAsync` (`All`/`Any`) replace the whole collection
  server-side — send the complete set, not a delta.
- **Attachments**: upload bytes via `Uploads.UploadAsync` (raw binary, 50 MB cap, tokens expire after
  60 minutes and are single-use), then attach the token through `ZendeskTicketCommentWrite.Uploads`.
- **Retries duplicate writes**: Zendesk has no idempotency keys. Under Ignite, the standard resilience
  handler retries transient failures — a retried `POST` that actually reached Zendesk can create a
  duplicate. Keep that in mind for unattended bulk creates.
- **Deliberately not implemented** (deprecated or gated): legacy string comment redaction, the
  `users/me/merge` endpoint (removed), user password endpoints (off by default, no OAuth-scope
  support), and legacy-CSAT satisfaction-rating creation.

## Configuration

`ZendeskClientOptions` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `Subdomain` | `string` | `""` | The `{subdomain}` in `https://{subdomain}.zendesk.com`. Must be a plain DNS label. |
| `BaseUrl` | `string?` | `null` | Explicit base URL override (sandbox, test double). Takes precedence over `Subdomain`; must be absolute `http(s)`. |
| `OAuth` | `ZendeskOAuthOptions` | — | The OAuth `client_credentials` settings below. |

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

Any non-success Zendesk response throws a `ZendeskApiException`:

| Member | Type | Purpose |
| --- | --- | --- |
| `StatusCode` | `HttpStatusCode` | The HTTP status Zendesk returned. |
| `ResponseBody` | `string?` | A bounded prefix (≤ 2 KiB) of the raw response body — Zendesk's actual error JSON. |
| `RetryAfter` | `TimeSpan?` | The `Retry-After` hint, when present (typically on `429 Too Many Requests`). |

```csharp
try
{
    var ticket = await zendesk.Tickets.GetByIdAsync(ticketId, cancellationToken);
}
catch (ZendeskApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
{
    // Unknown ticket id.
}
catch (ZendeskApiException exception) when (exception.StatusCode == HttpStatusCode.TooManyRequests)
{
    var wait = exception.RetryAfter ?? TimeSpan.FromSeconds(30);
    // Back off and retry / surface the wait to the caller.
}
```

Operations that Zendesk answers with an empty envelope (e.g. a missing record inside a `200` response)
throw `InvalidOperationException` with the operation and id in the message.

### Rate limits and resilience

Zendesk enforces per-minute rate limits and answers `429` with `Retry-After`. The client itself does
not retry — pair it with a resilience handler:

- **Under Ignite** this is automatic: `builder.Ignite()` applies the standard resilience handler
  (which honors `Retry-After`) to every `HttpClient` by default.
- **Standalone**, chain it on the returned builder
  (requires the `Microsoft.Extensions.Http.Resilience` package):

```csharp
builder.Services
    .AddZendeskClient(options => { /* … */ })
    .AddStandardResilienceHandler();
```

## Limits

Two kinds of caps exist — client-side ones (ours, graceful) and Zendesk's server-side ones (HTTP errors):

| Limit | Enforced by | Behavior when hit |
| --- | --- | --- |
| Attachment download cap (opt-in via `maxContentBytes`; **unlimited by default**) | **Client** | When a cap is set: stops reading, `Truncated = true`, UTF-8-safe trim for text |
| Error-body capture (2 KiB) | **Client** | Bounds `ZendeskApiException.ResponseBody` only — diagnostics, never data |
| Comment `bodyFormat` trimming | **Client** | Drops one body representation after receipt (`both` keeps everything) |
| Bulk writes: 1–100 items | **Zendesk rule, client pre-flight** | `ArgumentException` before the request (Zendesk would answer `400`); batch *reads* chunk instead of rejecting |
| Upload file size (50 MB) | Zendesk | Server rejection |
| `per_page` / `page[size]` max 100 | Zendesk | Clamped/rejected by the API |
| Offset paging: 100 pages / 10,000 records | Zendesk | `400` beyond — use cursor pagination |
| `/search`: first 1,000 results only | Zendesk | Use `Search.ExportTicketsAsync` (uncapped, cursor) |
| 5,000 comments per ticket; 64 KB per comment | Zendesk | `422` on writes beyond |
| Upload tokens: 60 min, single-use | Zendesk | `4xx` on reuse/expiry |
| Rate limits (account-wide + per-endpoint) | Zendesk | `429`; `RetryAfter` on the exception, auto-retried under Ignite |

## Observability

### Tracing

Every operation runs inside a client `Activity` on the `ES.FX.Zendesk` `ActivitySource`
(`ZendeskClientInstrumentation.ActivitySourceName`), named `Zendesk.{Area}.{Operation}` (e.g.
`Zendesk.Tickets.Search`), with status and exception details recorded on failure. The
[Zendesk Spark](../ignite/sparks/zendesk.md) wires the source into OpenTelemetry for you; standalone:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(ZendeskClientInstrumentation.ActivitySourceName));
```

### Logging

The client logs through the standard `ILogger` pipeline: `Debug` on success, `Warning` on failure
(with the operation and status code), and nothing on caller-initiated cancellation. Response bodies are
**never** logged — they can contain requester PII; they remain available on
`ZendeskApiException.ResponseBody`.

### Metrics

The client adds no custom meter — .NET's built-in `http.client.*` metrics already cover request
counts and durations for the underlying `HttpClient`.

## See also

- [Zendesk Spark](../ignite/sparks/zendesk.md) — the Ignite integration: config binding, health check, tracing.
- [Framework libraries](./index.md)
- [Ignite overview](../ignite/index.md)
- [Zendesk API reference](https://developer.zendesk.com/api-reference/)
- [Zendesk OAuth client_credentials grant](https://developer.zendesk.com/documentation/ticketing/working-with-oauth/using-oauth-authentication-with-your-application/)
