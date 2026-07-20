---
title: Zendesk MCP server
description: A deployable Model Context Protocol server exposing Zendesk Support and Help Center — 215 read and write tools built on the Kiota-generated ES.FX.Zendesk clients — over Streamable HTTP, with lean-first responses, execution-mode gating, and Origin validation.
---

## Overview

`ES.FX.Zendesk.MCP.Host` is a runnable [Model Context Protocol](https://modelcontextprotocol.io) (MCP)
server that exposes Zendesk Support as MCP tools an AI agent can call. It is built on the
[Kiota-generated ES.FX.Zendesk clients](./zendesk-client.md) and publishes **215 tools** — **100 read**
and **115 write** (32 of the writes destructive) — spanning twenty resource areas. Tool responses are
**lean-first**: list/search tools return per-entity allowlist **summary rows** inside a uniform
envelope, the per-record `*_get` tools return the **complete record** (minus API self-links and
null-valued fields), and write tools return **minimal confirmations** — with every omission carrying an
explicit, reachable escalation path (see [Lean responses](#lean-responses)). Requests are still built
wire-true with the generated request builders, and the fields that *are* returned keep the names and
values documented at [developer.zendesk.com](https://developer.zendesk.com/api-reference/) — the tools
shape *how much* of a payload comes back, never what its fields mean.

Unlike the rest of `ES.FX.*`, this is a **deployable application, not a NuGet package** (it sets
`IsPackable=false` / `GeneratePackageOnBuild=false`). It is built on [Ignite](../ignite/index.md) and the
official [`ModelContextProtocol.AspNetCore`](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore)
SDK, and is served over the Streamable HTTP transport.

The Zendesk vertical spans three pieces; this page covers the third:

| Piece | Package / app | Page |
| --- | --- | --- |
| Generated API clients | `ES.FX.Zendesk` | [Zendesk API client](./zendesk-client.md) |
| Ignite integration | `ES.FX.Ignite.Zendesk` | [Zendesk Spark](../ignite/sparks/zendesk.md) |
| **MCP server** | `ES.FX.Zendesk.MCP.Host` (app) | **this page** |

> [!NOTE]
> The MCP server is wired directly inside the host (`Hosting/McpServerHostingExtensions.cs`,
> `builder.AddZendeskMcpServer()` + `app.MapZendeskMcp()`) rather than shipped as a reusable Spark or
> package — an MCP server is only meaningful as part of a concrete deployment, so there is no
> `ES.FX.Ignite.ModelContextProtocol` package.

## Architecture

The host is a thin composition layer. Its `Program.cs` follows the standard
[`ProgramEntry`](../development/hosting.md) + two-phase [Ignite](../ignite/index.md) shape:

```csharp
return await ProgramEntry.CreateBuilder(args).UseSerilog().Build().RunAsync(async _ =>
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders();
    builder.IgniteSerilog();
    builder.Ignite();                 // OpenTelemetry, health checks, resilient HttpClient
    builder.IgniteZendeskClient();    // the Zendesk Spark: generated clients + live health check + tracing

    var areaGate = ZendeskToolAreaGate.FromConfiguration(
        builder.GetMcpOptions().Tools.Areas, typeof(Program).Assembly);

    builder.AddZendeskMcpServer()     // MCP server (Streamable HTTP) + execution-mode services
        .WithToolsInArea<ZendeskUserTools>(areaGate)
        // … 19 read tool classes, each subject to the Mcp:Tools:Areas gate;
        //    12 write tool classes only when the baseline execution mode allows writes
        ;

    var app = builder.Build();
    app.Ignite();
    app.MapZendeskMcp();              // Origin-validation middleware + MapMcp
    await app.RunAsync();
    return 0;
});
```

Each tool is a method on a `[McpServerToolType]` class (one class per resource area, read and write split
into separate classes). Tools build their requests with the generated Kiota request builders and send
them as raw JSON round-trips — the generated models drop spec-`readOnly` fields (ids, timestamps,
counts, cursors) on re-serialization, so typed round-trips are deliberately avoided (see the
[re-serialization hazard](./zendesk-client.md#wire-true-json-and-the-re-serialization-hazard)). Every
client call is routed through a single `ZendeskToolInvoker` so error handling and execution-mode gating
are uniform (see [Error handling](#error-handling) and [Execution modes](#execution-modes)).

## Transport and endpoint

The server uses the **Streamable HTTP** transport. Two options (bound from the `Mcp` configuration section)
shape it:

| Key | Default | Meaning |
| --- | --- | --- |
| `Mcp:Endpoint` | `""` (application root) | Route pattern the MCP endpoints are mapped at. |
| `Mcp:Stateless` | `true` | Run statelessly (no `Mcp-Session-Id`), so the server scales horizontally. |

## Execution modes

The server enforces a baseline **execution mode** that governs whether write tools may run. A per-request
header may make the mode *more* restrictive, but **never less** — a read-only deployment can never be talked
into writing.

| Mode | Read tools | Write tools |
| --- | --- | --- |
| `Default` | run | perform their changes |
| `DryRun` | run | **not executed** — return an explicit `{ "status": "dry_run", "executed": false, … }` payload describing the change that *would* have been made: single-entity writes echo the request verbatim, bulk `*_many` writes return a compact digest (see [Lean responses](#lean-responses)) |
| `ReadOnly` | run | **not registered** (as the baseline) / **rejected** with an error (when tightened per request) |

- **Configuration:** `Mcp:Execution:Mode` (`Default` \| `DryRun` \| `ReadOnly`) sets the baseline.
- **Per-request tightening:** a request header (`Mcp:Execution:HeaderName`, default `X-Mcp-Execution-Mode`,
  values `dry-run` / `read-only`) can raise restrictiveness. Disable with `Mcp:Execution:AllowHeaderOverride = false`.
- **Fail-closed:** if the header is present but unparseable (or duplicated into an ambiguous value), the
  server resolves to `ReadOnly` rather than silently falling back to the less-restrictive baseline.
- **Registration gating:** when the baseline is `ReadOnly`, the write tool classes are **not registered at
  all**, so the advertised tool list is genuinely read-only. In `Default`/`DryRun` the write tools are
  registered and `ZendeskToolInvoker` enforces the effective per-request mode on every call.

## Configuration

Zendesk credentials live under `Ignite:Zendesk` (the [Spark](../ignite/sparks/zendesk.md) convention);
server behavior lives under `Mcp`. All keys are environment-overridable (`__` maps to `:`).

```jsonc
{
  "Ignite": {
    "Zendesk": {
      "Subdomain": "acme",              // -> https://acme.zendesk.com/api/v2/
      "OAuth": {                        // OAuth 2.0 client_credentials (confidential client)
        "ClientId": "***",
        "ClientSecret": "***",
        "Scope": "read write"           // full tool surface; drop "write" for a read-only client
      }
    }
  },
  "Mcp": {
    "Endpoint": "",                     // route prefix for the MCP endpoints ("" = root)
    "Stateless": true,                  // Streamable HTTP, horizontally scalable
    "AllowedOrigins": [],               // browser Origins allowed through (empty = reject all; see Security)
    "Execution": {
      "Mode": "Default",                // Default | DryRun | ReadOnly
      "AllowHeaderOverride": true,
      "HeaderName": "X-Mcp-Execution-Mode"
    },
    "Tools": {
      "Areas": [],                      // [] = all areas; e.g. ["tickets","users"] registers only those. See Filtering.
      "MaxResponseChars": 60000,        // response-size budget (serialized chars) per tool response; min 1000
      "MaxResponseCharsByTool": {}      // per-tool overrides, keyed by tool name (case-insensitive)
    }
  }
}
```

The OAuth model, keyed multi-tenant instances, and secret hygiene are covered on the
[Zendesk API client](./zendesk-client.md#authentication) and [Zendesk Spark](../ignite/sparks/zendesk.md)
pages. **Write tools require the OAuth scope to include `write`** (`"read write"`).

## Filtering the tool surface

MCP clients filter tools by **exact name only** — there is no glob, prefix, or regex matching, and clients
(Hermes, Claude, and others) do **not** consult the MCP `readOnlyHint` / `destructiveHint` annotations when
deciding what to expose. So narrowing the surface to "only reads", "only tickets", or "only ticket reads" is
best done **server-side**, with a client include-list as the fallback when you cannot change the deployment.
Three levers, in order of preference:

1. **Read-only deployment** — set `Mcp:Execution:Mode = ReadOnly`. The write tool classes are never
   registered, so the advertised surface is exactly the 100 read tools. No client config required.
2. **Area gating** — set `Mcp:Tools:Areas` to the resource areas you want. Only tool classes in those areas
   are registered, and it composes with the execution mode via **AND**:
   - `Areas: ["tickets"]` → all ticket tools (reads **and** writes).
   - `Areas: ["tickets"]` with `Mode: ReadOnly` → only ticket **read** tools.

   An empty/absent list registers all areas (backward compatible). An **unknown area fails startup**
   (fail-closed) with the list of valid areas, rather than silently exposing nothing. Valid areas:
   `tickets`, `ticket_fields`, `users`, `organizations`, `groups`, `articles`, `macros`, `forms`, `views`,
   `brands`, `custom_statuses`, `job_statuses`, `tags`, `suspended_tickets`, `attachments`, `uploads`,
   `search`, `satisfaction_ratings`, `community`, `custom_objects`.
3. **Client include-list** — when you cannot change the deployment, list exact tool names in the client
   config. The ready-made profiles below are snapshot-tested in the host test project
   (`ZendeskToolProfileSnapshotTests`), so they never drift from the real tool surface.

> [!TIP]
> Prefer the server-side levers (1 and 2) when you control the deployment: a read-only or area-scoped server
> *cannot* be talked into exposing more, whereas a client include-list is only as good as the client's config.

### Hermes example

Hermes matches `tools.include` by exact tool name (and `include` wins over `exclude`):

```yaml
mcp_servers:
  zendesk:
    url: "http://localhost:8080"
    tools:
      # Allow reading tickets and adding internal notes / setting status, but NOT replying to the customer
      # (tickets_reply_public is omitted) — the single-action write tools make per-action gating possible.
      include: [tickets_get, tickets_search, tickets_comments_list, tickets_note_add, tickets_status_set]
```

### Include-list profiles

One tool name per line; kept in sync with the source by `ZendeskToolProfileSnapshotTests`.

**Only ticket reads** — equivalent to `Areas: ["tickets"]` + `Mode: ReadOnly`:

```
tickets_audits_list
tickets_collaborators_list
tickets_comments_count
tickets_comments_list
tickets_count
tickets_export_incremental
tickets_get
tickets_get_by_external_id
tickets_get_many
tickets_incidents_list
tickets_list
tickets_metric_events_export
tickets_metrics_get
tickets_search
tickets_search_export
tickets_side_conversations_list
```

<details>
<summary><strong>All read tools</strong> (100) — equivalent to <code>Mode: ReadOnly</code></summary>

```
articles_categories_get
articles_categories_list
articles_deflection_search
articles_get
articles_list
articles_search
articles_sections_get
articles_sections_list
attachments_get
brands_get
brands_list
community_posts_search
custom_objects_list
custom_objects_records_get
custom_objects_records_list
custom_objects_records_search
custom_statuses_get
custom_statuses_list
forms_get
forms_list
groups_assignable_list
groups_count
groups_get
groups_list
groups_memberships_list
groups_users_count
groups_users_list
job_statuses_get
job_statuses_get_many
job_statuses_list
macros_changes_get
macros_get
macros_list
macros_list_active
macros_search
macros_ticket_preview_get
organizations_autocomplete
organizations_count
organizations_get
organizations_get_by_name_or_external_id
organizations_get_many
organizations_list
organizations_memberships_list
organizations_merges_get
organizations_tags_list
organizations_tickets_count
organizations_tickets_list
organizations_users_count
organizations_users_list
satisfaction_ratings_count
satisfaction_ratings_get
satisfaction_ratings_list
search_count
suspended_tickets_get
suspended_tickets_list
tags_autocomplete
tags_count
tags_list
ticket_fields_get
ticket_fields_get_many
ticket_fields_list
ticket_fields_options_list
tickets_audits_list
tickets_collaborators_list
tickets_comments_count
tickets_comments_list
tickets_count
tickets_deleted_list
tickets_export_incremental
tickets_get
tickets_get_by_external_id
tickets_get_many
tickets_incidents_list
tickets_list
tickets_metric_events_export
tickets_metrics_get
tickets_search
tickets_search_export
tickets_side_conversations_list
users_autocomplete
users_count
users_get
users_get_many
users_groups_list
users_identities_list
users_list
users_me_get
users_organizations_list
users_related_get
users_search
users_tags_list
users_tickets_assigned_list
users_tickets_ccd_list
users_tickets_requested_list
views_count
views_count_many
views_get
views_list
views_rows_list
views_tickets_list
```
</details>

### Context cost & client requirements

Every tool advertised by the server ships its full name, description, and JSON input schema in the
`tools/list` reply the client reads at connect time — and, on most clients, that entire block is injected
into the model's context on **every turn**. At the full surface of **215 tools** that description text is a
standing token tax the agent pays before it has read a single ticket, and it scales with the number of
servers a client has connected. Two client behaviors bound the impact:

- **Clients with deferred tool loading** (they advertise only tool *names* up front and fetch a tool's full
  schema on first use) pay a small tax regardless of surface size — the 215 tools cost roughly 215 names.
- **Clients without it** materialize all 215 descriptions on every turn; here the surface size is the tax,
  and trimming it is the single biggest context lever you have.

Because the tax is paid client-side but the surface is chosen server-side, **the primary context lever is
server-side gating, not client filtering**:

- Set `Mcp:Execution:Mode = ReadOnly` to drop the 115 write tools from `tools/list` entirely (down to the 100
  reads), and/or scope `Mcp:Tools:Areas` to the resource areas the deployment actually needs (see
  [Filtering the tool surface](#filtering-the-tool-surface)). A read-only, `Areas: ["tickets"]` server
  advertises ~16 tools instead of 215 — an order-of-magnitude smaller `tools/list` for **every** connected
  client, with no per-client configuration to keep in sync.
- A [client include-list](#filtering-the-tool-surface) is the fallback when you cannot change the
  deployment, but it only shrinks what the client *chooses to expose* — it never changes what the server
  advertises, and it drifts per client.

> [!NOTE]
> The write surface deliberately goes the **opposite** way from consolidation: the former composite
> `tickets_update` / `users_update` / `organizations_update` tools were **decomposed** into single-action
> setters (e.g. `tickets_reply_public` vs `tickets_note_add`, `users_role_set`) so a deployment can grant or
> deny individual actions via the client include-list. Collapsing the surface the other way by
> **consolidating CRUD verbs** (one `tickets` tool taking an `action` parameter) is **deferred by design, not
> adopted**. It would cut the tool count but at the cost of the risk-legible naming and
> annotations this server relies on — a tool's read/write/destructive class is currently readable from its
> name and enforced by [`ReadOnly`/`Destructive` annotations](#tools), which a single multiplexed tool
> erases. The gating levers above already buy the context savings without that trade-off; revisit
> consolidation only behind a measured pilot on a client that proves the annotations are not load-bearing.

## Security (deployment)

> [!WARNING]
> The MCP endpoint is mapped **without authentication** (`app.MapMcp(...)` has no `RequireAuthorization()`),
> so any caller that can reach it invokes tools using the server's single set of Zendesk credentials. The
> execution-mode gate constrains *what* a caller may do, not *who* they are.

Before exposing the server:

- Bind it to a trusted network only (private interface / service mesh), **or**
- Front it with authentication (API gateway, mTLS, or the MCP SDK's bearer/OAuth resource-server support)
  and add `.RequireAuthorization()` to the mapped endpoint.

**Origin validation (DNS rebinding).** As required by the MCP Streamable HTTP transport specification, the
host validates the `Origin` header: a request whose `Origin` is not in `Mcp:AllowedOrigins` is rejected with
`403`. The default (empty list) rejects **all** browser origins; non-browser clients (agents, CLIs) send no
`Origin` header and always pass. Add explicit origins only if a browser-based client must connect.

The Ignite health endpoints (`/health/live`, `/health/ready`) are likewise unauthenticated — keep them off
any public interface.

## Error handling

Zendesk failures reach the agent with the real HTTP status and response body rather than the SDK's opaque
generic error. `ZendeskToolInvoker` catches both exception types of the
[client's error model](./zendesk-client.md#error-handling) — the typed `ZendeskApiException` (non-retryable
4xx, carrying Zendesk's error body) and Kiota's `ApiException` (retry-exhausted `408`/`429`/`5xx`, with
`Retry-After` recovered from the response headers) — and rethrows each as an `McpException` whose message
the SDK surfaces verbatim, so an agent can distinguish `404` from `403` from `422`, self-correct, and honor
an explicit "Retry after N seconds" hint on `429`.

## Lean responses

Responses are **lean-first**: every tool returns the minimum an agent needs to triage or act, and every
omission carries an explicit, reachable escalation path. Zendesk has no field-selection mechanism
anywhere in the Support or Help Center APIs, so the projection happens server-side in the host (the
`ZendeskLean` helper); requests stay wire-true. The contract is stated in each tool's description and
reinforced by the server-level MCP `instructions` sent to clients at initialize:

> Zendesk MCP server — lean response contract: list/search tools return summary rows by default; pass
> detail:'full' on the list tool, or call the record's \*_get tool, for a complete record. An absent
> field means null/empty, not unknown. Prefer the \*_count tools to answer how-many questions, and
> tickets_metrics_get for ticket timing metrics. A response's 'note' field carries dynamic conditions:
> continuation (with has_more and after_cursor/next_page), truncation, and omitted data with the exact
> re-call that retrieves it.

### Summary rows and the `detail` parameter

List/search/sublist tools take a `detail` parameter (default `"summary"`):

| Value | Rows |
| --- | --- |
| `summary` (alias `concise`) | Per-entity **allowlist** rows — only the fields needed to triage or pick a record. |
| `full` (aliases `detailed`, `verbose`) | Complete Zendesk objects, minus null-valued fields and API self-links. |

The value is **validated**, case-insensitively: anything other than the values and aliases above is
rejected with an error naming the allowed values — never silently coerced (`articles_get`'s
`bodyFormat` gets the same treatment). Summary shapes strip the token-heavy members and point at the
sink that still carries them: macro **actions**, view **conditions**, ticket-form **condition trees**,
brand **logos**, and a suspended ticket's **raw email content** are dropped; a ticket field's options
collapse to a computed `options_count`; a ticket's `description` is a 150-character excerpt (the
description *is* the first comment); audit Comment events collapse their triple body duplication to a
single 200-character excerpt.

### `*_get` tools are the detail sinks

The per-record `*_get` tools take no `detail` parameter — they always return the **full view**: the
complete Zendesk object minus `url` API self-links, null-valued fields, and absolute
`next_page`/`previous_page`/`links` pagination URLs. `html_url` — the human permalink — is always kept.
The absence convention holds everywhere: **an absent field means null/empty, not unknown**.

### The list envelope

Every list/search tool returns one uniform envelope — **metadata first, items last**, so an agent
reading a truncated stream still sees the contract:

```jsonc
{
  "detail": "summary",        // the detail level applied to the rows
  "count": 123,               // only when Zendesk supplied a total
  "has_more": true,
  "after_cursor": "…",        // cursor tools — OR next_page (a page NUMBER) for offset tools, never both
  "note": "…",                // dynamic conditions only: continuation, truncation, omitted data
  "items": [ … ],             // the projected rows
  "users": [ … ]              // requested sideloads, under their native Zendesk names, summary-projected
}
```

A tool emits exactly one continuation kind: cursor-paginated tools carry `after_cursor`, offset tools
carry `next_page` as a computed page *number* — Zendesk's absolute URL strings are never parsed or
echoed. Sideloads without a registered summary shape fail **visibly**: the array is omitted and the
`note` says `sideload X has no summary shape — use detail:'full'`.

### Page-size defaults

No list tool leaves the page size to Zendesk's server default of 100 — every default is explicit on the
wire and stated in the tool's description:

| Default | Tools |
| --- | --- |
| 25 | Entity lists — `tickets_list`, `tickets_search`, `users_list`, `organizations_list`, `groups_list`, `views_list`, `macros_list`, `articles_list`, and the other list/sublist tools not named below. |
| 10 | `tickets_comments_list`, `tickets_audits_list`, `users_autocomplete`, `organizations_autocomplete`, `articles_search` — heavy rows or prefix lookups. |
| 20 | `job_statuses_list` |
| 30 | `articles_sections_list`, `articles_categories_list` |
| 50 | `tags_list`, `ticket_fields_list` — small rows. |
| 100 | `tickets_export_incremental`, `tickets_search_export`, `ticket_fields_options_list` — deep-export/decode paths. |

`*_get_many` tools accept a **hard cap of 100 ids per call** (Zendesk's `show_many` contract); more ids
are rejected with an actionable batching error rather than fanned out silently.

### Truncation markers

A capped long value ends with a self-describing marker naming the **exact re-call** that retrieves the
untruncated content — for example a comment body capped by `tickets_comments_list`'s `maxBodyChars`
(default 2,000) ends with:

```
…[truncated N chars — re-call with maxBodyChars:0 (0 = no limit), perPage:1, page:<n> for this comment]
```

Summary-row excerpts (ticket descriptions, audit comment excerpts) use a bare trailing `…` — the row's
description already names the sink (`tickets_get`, `tickets_comments_list`).

### Response-size guard

A safety net behind the projection (rarely hit): `Mcp:Tools:MaxResponseChars` (default 60,000
serialized characters, minimum 1,000) with per-tool overrides in `Mcp:Tools:MaxResponseCharsByTool`,
keyed by tool name (case-insensitive) — e.g. `Mcp:Tools:MaxResponseCharsByTool:tickets_audits_list =
90000`. A **list** response over the budget drops tail items, **suppresses the continuation token**
(resuming from it would silently skip the dropped items), forces `has_more: true`, and puts the
recovery recipe in the note (`items 19–25 of this page were dropped — re-call with perPage:18 or a
narrower query`). A **non-list** response over the budget fails with an error naming that tool's actual
narrowing parameters. Tools with their own explicit size caps (`attachments_get`'s `maxBytes`) are
exempt by design.

### Write confirmations

Write tools return minimal confirmations instead of echoing the mutated record:

- **create** → `{id, a few identity fields, created_at}` — e.g. `tickets_create` returns
  `{id, subject, status, created_at, audit_id}`.
- **update** → `{id, updated_at}` **plus the server-state values of exactly the fields sent** — the
  echo-of-change: a returned value differing from what was sent reveals a trigger/business-rule
  override without a follow-up `*_get`.
- **create-or-update** (upserts) → additionally `created: true|false`, captured from the real HTTP
  status code.
- **delete / acknowledge** → an explicit acknowledgement carrying the affected id(s).
- **bulk `*_many`** → the queued job's `{id, status}` — poll `job_statuses_get`, whose summary
  collapses per-item results to a `results_summary` (`succeeded`/`failed` counts plus the first
  failures).

Under **dry-run**, single-entity writes echo the request verbatim (small, and the echo is the
verification value); bulk `*_many` writes return a compact digest — `{action, target, count, items}`
with per-item ids and changed field names, long values truncated — instead of up to 100 echoed models.

### The cheap-path count and batch tools

Four tools exist specifically to close expensive access patterns:

| Tool | Replaces | Caveat |
| --- | --- | --- |
| `organizations_tickets_count` | Paging `organizations_tickets_list` to size a history. | Counts above 100,000 are cached: refreshed only every ~24 hours and capped at 100,000 until the refresh completes (`refreshed_at` reports the cache time and may be null in that window). For an exact filtered count use `search_count`. |
| `organizations_users_count` | Paging `organizations_users_list` to size a membership. | Same approximation/staleness caveat as above. |
| `groups_users_count` | Paging `groups_users_list` to size a team. | Same approximation/staleness caveat as above. |
| `ticket_fields_get_many` | The 1+N loop of `ticket_fields_get` per `custom_fields[].id`. | Full-detail rows; ≤ 100 ids per call; options capped at 100 per field with an `options_truncated` marker pointing at `ticket_fields_options_list`. |

## Tools

215 tools, named resource-first as `{area}[_{subresource}]_{verb}[_{qualifier}]` — snake_case, with **no
product prefix** (MCP clients already namespace tools by server, so a `zendesk_` prefix would only stutter as
`mcp_zendesk_zendesk_…`). The **area** leads so related tools sort together, and every read tool ends in a
controlled read verb (`get` / `list` / `search` / `count` / `export` / `autocomplete`) while any other verb
denotes a write — so a tool's risk class is legible from its name alone. This is machine-enforced: a test
(`ZendeskToolAnnotationSweepTests`) asserts the read-verb suffix matches the `ReadOnly` annotation and that
destructive verbs (`delete` / `merge` / `redact` / `mark_spam`) carry `Destructive = true`. Conventions shared
by all tools:

- **Pagination** — offset-paginated tools take `page`/`perPage`; cursor-paginated tools take
  `pageSize`/`afterCursor`. Either way, the [list envelope](#the-list-envelope) reports `has_more` plus
  exactly one continuation field: `next_page` (a page number) or `after_cursor`.
- **Sideloads** — list/read tools accept an `include` parameter where the Zendesk endpoint supports it,
  returning related records as summary-projected sibling arrays to avoid per-id follow-up calls.
- **Bulk writes** — bulk operations (≤100 items unless noted) return a `job_status`; poll
  `job_statuses_get` until it reports `completed`/`failed`.

### Read tools (100 total)

Every read tool is annotated `ReadOnly = true` and is available in all execution modes.

#### Users

| Tool | What it does |
| --- | --- |
| `users_me_get` | Returns the Zendesk user associated with the configured credentials. |
| `users_get` | Returns a Zendesk user by id. |
| `users_search` | Searches Zendesk users. |
| `users_get_many` | Returns many users by id in one call (batch resolution). |
| `users_tickets_requested_list` | Returns the tickets a user has requested (their ticket history). |
| `users_list` | Lists Zendesk users, optionally filtered by role. |
| `users_count` | Returns the (cached, approximate) user count, optionally filtered by role. |
| `users_autocomplete` | Suggests users whose name or e-mail starts with a prefix. |
| `users_related_get` | Returns a user's related ticket/subscription counts. |
| `users_identities_list` | Lists a user's identities (e-mails, phone numbers, social handles). |
| `users_groups_list` | Lists the groups an agent belongs to. |
| `users_organizations_list` | Lists the organizations a user belongs to. |
| `users_tickets_assigned_list` | Returns the tickets assigned to an agent. |
| `users_tickets_ccd_list` | Returns the tickets a user is CC'd on. |
| `users_tags_list` | Lists a user's tags. |

#### Tickets

| Tool | What it does |
| --- | --- |
| `tickets_get` | Returns a Zendesk ticket by id. |
| `tickets_search` | Searches Zendesk tickets. |
| `tickets_search_export` | Exports ticket search results with cursor pagination (no 1,000-result cap). |
| `tickets_comments_list` | Returns the conversation thread (comments) on a ticket. |
| `tickets_audits_list` | Returns a ticket's change history (audits/events). |
| `tickets_metrics_get` | Returns timing/lifecycle metrics for a ticket. |
| `tickets_incidents_list` | Returns the incidents linked to a problem ticket. |
| `tickets_side_conversations_list` | Returns a ticket's side conversations (vendor/escalation threads). |
| `tickets_metric_events_export` | Exports SLA/metric events across tickets (breach timeline). |
| `tickets_list` | Lists tickets. |
| `tickets_get_many` | Returns many tickets by id in one call. |
| `tickets_count` | Returns the account's total ticket count. |
| `tickets_get_by_external_id` | Returns the tickets carrying an external id. |
| `tickets_collaborators_list` | Lists the collaborators (CCs) of a ticket. |
| `tickets_comments_count` | Returns a ticket's comment count. |
| `tickets_export_incremental` | Exports tickets incrementally (cursor-based incremental export). |
| `tickets_deleted_list` | Lists soft-deleted tickets (the id source for `tickets_restore` / `tickets_delete_permanently`). |

#### Organizations

| Tool | What it does |
| --- | --- |
| `organizations_get` | Returns a Zendesk organization by id. |
| `organizations_tickets_list` | Returns the tickets belonging to an organization. |
| `organizations_tickets_count` | Returns an organization's ticket count (approximate/cached above 100,000). |
| `organizations_list` | Lists Zendesk organizations. |
| `organizations_count` | Returns the approximate organization count. |
| `organizations_get_many` | Returns many Zendesk organizations by id. |
| `organizations_get_by_name_or_external_id` | Looks up organizations by exact name or external id. |
| `organizations_autocomplete` | Suggests organizations whose name starts with a prefix. |
| `organizations_users_list` | Lists the users of an organization. |
| `organizations_users_count` | Returns an organization's user count (approximate/cached above 100,000). |
| `organizations_memberships_list` | Lists an organization's memberships. |
| `organizations_merges_get` | Returns an organization merge job's status. |
| `organizations_tags_list` | Lists an organization's tags. |

#### Groups

| Tool | What it does |
| --- | --- |
| `groups_list` | Lists Zendesk groups. |
| `groups_get` | Returns a Zendesk group by id. |
| `groups_memberships_list` | Lists the agents that belong to a group. |
| `groups_assignable_list` | Lists the groups assignable to tickets for the current agent. |
| `groups_count` | Returns the approximate group count. |
| `groups_users_list` | Lists the users of a group. |
| `groups_users_count` | Returns a group's user count (approximate/cached above 100,000). |

#### Help Center (articles / sections / categories)

| Tool | What it does |
| --- | --- |
| `articles_search` | Full-text searches Help Center knowledge base articles. |
| `articles_deflection_search` | Returns Guide's suggested (deflection) articles for a question — title + permalink, no body. |
| `articles_get` | Returns a single Help Center article including its full body. |
| `articles_list` | Lists Help Center articles, optionally scoped to a section. |
| `articles_sections_list` | Lists Help Center sections, optionally scoped to a category. |
| `articles_sections_get` | Returns a single Help Center section by id. |
| `articles_categories_list` | Lists Help Center categories. |
| `articles_categories_get` | Returns a single Help Center category by id. |

#### Ticket fields

| Tool | What it does |
| --- | --- |
| `ticket_fields_list` | Lists ticket field definitions. |
| `ticket_fields_get` | Returns a single ticket field definition by id. |
| `ticket_fields_get_many` | Returns many ticket field definitions by id in one call (decode a ticket's `custom_fields` without a per-field loop). |
| `ticket_fields_options_list` | Lists the custom options of a drop-down ticket field. |

#### Macros

| Tool | What it does |
| --- | --- |
| `macros_list` | Lists Zendesk macros. |
| `macros_list_active` | Lists only the macros usable by the current agent. |
| `macros_search` | Finds macros by title keyword and/or filters (access/active/category/group). |
| `macros_get` | Returns a single macro including its actions. |
| `macros_changes_get` | Previews the ticket field/comment changes a macro would make (not applied). |
| `macros_ticket_preview_get` | Previews a specific ticket as it would look after applying a macro (not applied). |

#### Ticket forms

| Tool | What it does |
| --- | --- |
| `forms_list` | Lists Zendesk ticket forms. |
| `forms_get` | Returns a Zendesk ticket form by id. |

#### Views

| Tool | What it does |
| --- | --- |
| `views_list` | Lists Zendesk views. |
| `views_get` | Returns a Zendesk view by id. |
| `views_tickets_list` | Returns the tickets currently matching a view. |
| `views_rows_list` | Runs a view as a work queue — rows rendered with the view's configured columns and sorting. |
| `views_count` | Returns the (cached) ticket count of a view. |
| `views_count_many` | Returns cached ticket counts for several views at once (bulk queue sizing). |

#### Search

| Tool | What it does |
| --- | --- |
| `search_count` | Returns the number of results a search query matches. |

#### Brands

| Tool | What it does |
| --- | --- |
| `brands_list` | Lists Zendesk brands. |
| `brands_get` | Returns a Zendesk brand by id. |

#### Custom statuses

| Tool | What it does |
| --- | --- |
| `custom_statuses_list` | Lists Zendesk custom ticket statuses. |
| `custom_statuses_get` | Returns a Zendesk custom ticket status by id. |

#### Job statuses

| Tool | What it does |
| --- | --- |
| `job_statuses_list` | Lists recent Zendesk job statuses. |
| `job_statuses_get` | Returns a Zendesk job status by id. |
| `job_statuses_get_many` | Returns many Zendesk job statuses in one request. |

#### Tags

| Tool | What it does |
| --- | --- |
| `tags_list` | Lists the most popular Zendesk tags with usage counts. |
| `tags_count` | Returns the account-wide tag count. |
| `tags_autocomplete` | Suggests Zendesk tag names matching a prefix. |

#### Suspended tickets

| Tool | What it does |
| --- | --- |
| `suspended_tickets_list` | Lists Zendesk suspended tickets. |
| `suspended_tickets_get` | Returns a Zendesk suspended ticket by id. |

#### Attachments

| Tool | What it does |
| --- | --- |
| `attachments_get` | Downloads an attachment's content (text inline, binary as size-capped base64). |

#### Satisfaction ratings

| Tool | What it does |
| --- | --- |
| `satisfaction_ratings_list` | Lists CSAT satisfaction ratings, filterable by score and time window. |
| `satisfaction_ratings_get` | Returns a single CSAT satisfaction rating by id. |
| `satisfaction_ratings_count` | Returns the CSAT satisfaction rating count. |

#### Community (Gather)

| Tool | What it does |
| --- | --- |
| `community_posts_search` | Full-text searches Help Center community (Gather) posts — peer discussions and workarounds. |

#### Custom objects

| Tool | What it does |
| --- | --- |
| `custom_objects_list` | Lists the tenant's custom object types (discover the `key` for the record tools). |
| `custom_objects_records_list` | Lists records of a custom object type (the business data), optionally filtered by external id. |
| `custom_objects_records_search` | Free-text searches records of a custom object type. |
| `custom_objects_records_get` | Returns a single custom object record by its (string) id. |

### Write tools (115 total)

Write tools are annotated `ReadOnly = false` and are gated by the [execution mode](#execution-modes):
rejected under `ReadOnly`, simulated under `DryRun`. Rows marked **destructive** carry `Destructive = true`
(they delete, purge, merge, redact, or otherwise irreversibly alter data). Bulk tools noted as "async job"
return a `job_status` — poll `job_statuses_get`.

#### Tickets

| Tool | Type | What it does |
| --- | --- | --- |
| `tickets_create` | write | Creates a Zendesk ticket. |
| `tickets_create_many` | write | Creates up to 100 Zendesk tickets as an async job. |
| `tickets_reply_public` | write | Adds a **public** reply (visible to the requester) to a ticket. |
| `tickets_note_add` | write | Adds an **internal** note (agents only) to a ticket. |
| `tickets_status_set` | write | Sets a ticket's status (+ optional custom status). |
| `tickets_priority_set` | write | Sets a ticket's priority. |
| `tickets_type_set` | write | Sets a ticket's type. |
| `tickets_assignee_set` | write | Assigns a ticket to an agent (+ optional group). |
| `tickets_group_set` | write | Routes a ticket to a group. |
| `tickets_requester_set` | write | Sets a ticket's requester. |
| `tickets_organization_set` | write | Sets a ticket's organization. |
| `tickets_form_set` | write | Sets a ticket's form. |
| `tickets_custom_fields_set` | write | Sets a ticket's custom field values. |
| `tickets_collaborators_set` | write | Sets a ticket's collaborators (CCs). |
| `tickets_due_at_set` | write | Sets a ticket's due date (task tickets). |
| `tickets_subject_set` | write | Sets a ticket's subject. |
| `tickets_status_set_many` | write | Sets the status of up to 100 tickets as an async job. |
| `tickets_assignee_set_many` | write | Assigns up to 100 tickets to an agent as an async job. |
| `tickets_group_set_many` | write | Routes up to 100 tickets to a group as an async job. |
| `tickets_tags_add_many` | write | Adds tags to up to 100 tickets as an async job (bulk tag append). |
| `tickets_tags_remove_many` | write | Removes tags from up to 100 tickets as an async job. |
| `tickets_note_add_many` | write | Adds an internal note to up to 100 tickets as an async job. |
| `tickets_reply_public_many` | write | Adds a public reply to up to 100 tickets as an async job. |
| `tickets_custom_fields_set_many` | write | Sets custom field values on up to 100 tickets as an async job. |
| `tickets_delete` | **destructive** | Soft-deletes a Zendesk ticket. |
| `tickets_delete_many` | **destructive** | Soft-deletes up to 100 tickets as an async job. |
| `tickets_merge` | **destructive** | Merges source tickets into a target ticket as an async job. |
| `tickets_mark_spam` | **destructive** | Marks a ticket as spam and suspends its requester. |
| `tickets_mark_spam_many` | **destructive** | Marks up to 100 tickets as spam as an async job. |
| `tickets_restore` | write | Restores a soft-deleted ticket. |
| `tickets_restore_many` | write | Restores up to 100 soft-deleted tickets. |
| `tickets_delete_permanently` | **destructive** | Permanently deletes an already soft-deleted ticket (irreversible). |
| `tickets_delete_permanently_many` | **destructive** | Permanently deletes up to 100 soft-deleted tickets as an async job (irreversible). |
| `tickets_tags_set` | write | Replaces a ticket's whole tag set. |
| `tickets_tags_add` | write | Adds tags to a ticket without removing existing ones. |
| `tickets_tags_remove` | write | Removes tags from a ticket. |
| `tickets_comments_make_private` | **destructive** | Makes a public ticket comment private (one-way). |
| `tickets_comments_attachment_redact` | **destructive** | Permanently redacts a comment attachment (irreversible). |
| `tickets_import` | write | Imports a historical ticket (admin-only; no triggers/notifications). |
| `tickets_import_many` | write | Imports up to 100 historical tickets as an async job (admin-only). |

#### Users

| Tool | Type | What it does |
| --- | --- | --- |
| `users_create` | write | Creates a Zendesk user. |
| `users_create_or_update` | write | Creates or updates a Zendesk user matched by e-mail or external id (upsert). |
| `users_create_many` | write | Creates up to 100 Zendesk users as an async job. |
| `users_create_or_update_many` | write | Creates or updates up to 100 Zendesk users as an async job. |
| `users_role_set` | write | Sets a user's role (privilege: end-user/agent/admin). |
| `users_suspended_set` | write | Suspends or unsuspends a user (access control). |
| `users_ticket_restriction_set` | write | Sets which tickets a user may access (permission). |
| `users_name_set` | write | Sets a user's display name. |
| `users_phone_set` | write | Sets a user's phone number. |
| `users_organization_set` | write | Sets a user's default organization (removes other memberships). |
| `users_notes_set` | write | Sets a user's agent-only notes and/or details. |
| `users_tags_set` | write | Replaces a user's whole tag set. |
| `users_fields_set` | write | Sets a user's custom field values. |
| `users_merge` | **destructive** | Merges one end user into another; the loser is absorbed and the winner survives. |
| `users_delete` | **destructive** | Soft-deletes a Zendesk user. |
| `users_delete_many` | **destructive** | Soft-deletes up to 100 Zendesk users as an async job. |
| `users_delete_permanently` | **destructive** | Permanently deletes an already soft-deleted Zendesk user (irreversible). |
| `users_identities_create` | write | Adds an identity (e-mail, phone, social handle) to a Zendesk user. |
| `users_identities_update` | write | Updates a Zendesk user identity's value or verification state. |
| `users_identities_make_primary` | write | Makes an identity the user's primary identity. |
| `users_identities_verify` | write | Marks a Zendesk user identity as verified. |
| `users_identities_request_verification` | write | Sends a verification e-mail for a Zendesk user identity. |
| `users_identities_delete` | **destructive** | Deletes a Zendesk user identity. |

#### Organizations

| Tool | Type | What it does |
| --- | --- | --- |
| `organizations_create` | write | Creates a Zendesk organization. |
| `organizations_create_many` | write | Creates up to 100 Zendesk organizations as an async job. |
| `organizations_create_or_update` | write | Creates or updates a Zendesk organization, matching by id or external id. |
| `organizations_name_set` | write | Sets an organization's name. |
| `organizations_domains_set` | write | Replaces an organization's domain-name list. |
| `organizations_notes_set` | write | Sets an organization's notes and/or details. |
| `organizations_tags_set` | write | Replaces an organization's tag set. |
| `organizations_fields_set` | write | Sets an organization's custom field values. |
| `organizations_sharing_set` | write | Sets an organization's ticket/comment sharing flags. |
| `organizations_delete` | **destructive** | Deletes a Zendesk organization by id (permanent; no restore). |
| `organizations_delete_many` | **destructive** | Deletes up to 100 Zendesk organizations as an async job (permanent). |
| `organizations_merge` | **destructive** | Merges one Zendesk organization into another (irreversible). |
| `organizations_memberships_create` | write | Links a user to an organization. |
| `organizations_memberships_create_many` | write | Creates up to 100 organization memberships as an async job. |
| `organizations_memberships_delete` | **destructive** | Removes an organization membership by its membership id. |
| `organizations_memberships_delete_many` | **destructive** | Removes up to 100 organization memberships as an async job. |
| `organizations_memberships_make_default` | write | Makes an organization membership the user's default. |

#### Groups

| Tool | Type | What it does |
| --- | --- | --- |
| `groups_create` | write | Creates a Zendesk group. |
| `groups_update` | write | Updates a Zendesk group by id. |
| `groups_delete` | **destructive** | Soft-deletes a Zendesk group by id. |
| `groups_memberships_create` | write | Assigns an agent to a group. |
| `groups_memberships_create_many` | write | Assigns up to 100 agents to groups as an async job. |
| `groups_memberships_delete` | **destructive** | Removes a group membership by its membership id. |
| `groups_memberships_delete_many` | **destructive** | Removes up to 100 group memberships as an async job. |
| `groups_memberships_make_default` | write | Makes a group membership the agent's default. |

#### Ticket forms

| Tool | Type | What it does |
| --- | --- | --- |
| `forms_create` | write | Creates a Zendesk ticket form. |
| `forms_update` | write | Updates a Zendesk ticket form. |
| `forms_delete` | **destructive** | Deletes a Zendesk ticket form. |
| `forms_clone` | write | Clones a Zendesk ticket form. |

#### Ticket fields

| Tool | Type | What it does |
| --- | --- | --- |
| `ticket_fields_create` | write | Creates a Zendesk ticket field. |
| `ticket_fields_update` | write | Updates a Zendesk ticket field. |
| `ticket_fields_delete` | **destructive** | Deletes a Zendesk ticket field (irreversible; strips its values from every ticket). |
| `ticket_fields_options_create_or_update` | write | Creates or updates a single custom field option on a drop-down ticket field. |
| `ticket_fields_options_delete` | **destructive** | Deletes a custom field option from a drop-down ticket field. |

#### Macros

| Tool | Type | What it does |
| --- | --- | --- |
| `macros_create` | write | Creates a Zendesk macro. |
| `macros_update` | write | Updates a Zendesk macro. |
| `macros_delete` | **destructive** | Deletes a Zendesk macro. |

#### Views

| Tool | Type | What it does |
| --- | --- | --- |
| `views_create` | write | Creates a Zendesk view. |
| `views_update` | write | Updates a Zendesk view by id. |
| `views_delete` | **destructive** | Deletes a Zendesk view by id. |

#### Brands

| Tool | Type | What it does |
| --- | --- | --- |
| `brands_create` | write | Creates a Zendesk brand. |
| `brands_update` | write | Updates a Zendesk brand by id. |
| `brands_delete` | **destructive** | Soft-deletes a Zendesk brand by id. |

#### Custom statuses

| Tool | Type | What it does |
| --- | --- | --- |
| `custom_statuses_create` | write | Creates a Zendesk custom ticket status. |
| `custom_statuses_update` | write | Updates a Zendesk custom ticket status by id. |
| `custom_statuses_delete` | **destructive** | Deletes a Zendesk custom ticket status by id. |

#### Suspended tickets

| Tool | Type | What it does |
| --- | --- | --- |
| `suspended_tickets_recover` | write | Recovers a suspended ticket into a real ticket. |
| `suspended_tickets_recover_many` | write | Recovers up to 100 suspended tickets, preserving their original requesters. |
| `suspended_tickets_delete` | **destructive** | Deletes a suspended ticket by id. |
| `suspended_tickets_delete_many` | **destructive** | Deletes up to 100 suspended tickets. |

#### Uploads

| Tool | Type | What it does |
| --- | --- | --- |
| `uploads_create` | write | Uploads a file (base64 content) and returns a token for attaching to a ticket comment. |
| `uploads_delete` | **destructive** | Deletes an unconsumed upload by its token. |

## Run

```bash
dotnet run --project src/ES.FX.Zendesk.MCP.Host        # http://localhost:8080
```

Health endpoints (from Ignite): `/health/live`, `/health/ready` (the latter live-pings Zendesk).

### Docker

```bash
# build from the repository root (build context = repo root)
docker build -f src/ES.FX.Zendesk.MCP.Host/Dockerfile -t es-fx-mcp-zendesk .
docker run --rm -p 8080:8080 \
  -e Ignite__Zendesk__Subdomain=acme \
  -e Ignite__Zendesk__OAuth__ClientId=*** \
  -e Ignite__Zendesk__OAuth__ClientSecret=*** \
  es-fx-mcp-zendesk
```

The image never contains development secrets: `appsettings.Development.json` is excluded from both the
build context (`.dockerignore`) and publish output (`CopyToPublishDirectory=Never`). Supply configuration
via environment variables or your orchestrator's secret store.

## Observability

Logging is via Serilog; OpenTelemetry traces and metrics are wired by [Ignite](../ignite/index.md). Both
Zendesk tracing sources (`ES.FX.Zendesk` and the Kiota adapter's
`Microsoft.Kiota.Http.HttpClientLibrary`) are registered by the Spark; the MCP SDK's
`ActivitySource`/`Meter` (`Experimental.ModelContextProtocol`) is registered by `AddZendeskMcpServer()`.

## See also

- [Zendesk API client](./zendesk-client.md) — the underlying generated clients, OAuth model, and error handling.
- [Zendesk Spark](../ignite/sparks/zendesk.md) — the Ignite integration this host builds on.
- [Application hosting](../development/hosting.md) — the `ProgramEntry` lifecycle wrapper used by the host.
- [Framework libraries](./index.md)
- [Model Context Protocol](https://modelcontextprotocol.io) — the protocol specification.
- [Zendesk API reference](https://developer.zendesk.com/api-reference/)
