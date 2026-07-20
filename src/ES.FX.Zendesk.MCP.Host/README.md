# ES.FX.Zendesk.MCP.Host

A [Model Context Protocol](https://modelcontextprotocol.io) (MCP) server that exposes Zendesk Support as
namespaced MCP tools — a **curated read and write surface** (**215 tools** across 20 resource areas)
built on the Kiota-generated `ES.FX.Zendesk` clients. Built on
**ES.FX.Ignite** and the official
[`ModelContextProtocol.AspNetCore`](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore) SDK, served
over the Streamable HTTP transport.

> This is a runnable application (not a published NuGet package). It consumes:
> - **`ES.FX.Zendesk`** — the reusable, HttpClientFactory-based Zendesk API clients: the Kiota-generated
>   `ZendeskSupportApiClient` / `ZendeskHelpCenterApiClient` plus the curated OAuth/error-handling rim
>   (supports multiple named/keyed instances).
> - **`ES.FX.Ignite.Zendesk`** — the Ignite Spark that binds/wires the clients (`builder.IgniteZendeskClient(name?, serviceKey?)`).
>
> The MCP server itself is wired directly in this app (`Hosting/McpServerHostingExtensions.cs`) rather than a
> standalone package, since an MCP server is only meaningful as part of a concrete implementation.

## Tools

**215 tools** named resource-first as `{area}[_{subresource}]_{verb}[_{qualifier}]` (snake_case, no product
prefix — MCP clients namespace by server): **100 read tools** (`ReadOnly = true`) and **115 write tools**
(`ReadOnly = false`, with truthful `Destructive`/`Idempotent` annotations). Every read tool ends in a
controlled read verb (`get`/`list`/`search`/`count`/`export`/`autocomplete`); any other verb denotes a write,
so a tool's risk class is legible from its name (and enforced by a test). Write tools are gated by the
[execution mode](#execution-modes-read-only--dry-run) and are **not registered at all** when the configured
baseline is `ReadOnly`, so a read-only deployment advertises a purely read-only tool list. The exposed surface
can be narrowed further by resource area via `Mcp:Tools:Areas` — see the
[filtering guide](../../docs/libraries/zendesk-mcp-server.md#filtering-the-tool-surface).

Conventions shared by all tools:

- **Lean responses** — list/search tools return allowlist summary rows in a uniform metadata-first envelope
  (`detail`/`count`/`has_more`/`after_cursor`|`next_page`/`note`/`items`); pass `detail:'full'`, or call the
  per-record `*_get` tool, for complete records. Write tools return minimal confirmations. See the
  [lean responses guide](../../docs/libraries/zendesk-mcp-server.md#lean-responses).
- **Pagination** — offset-paginated tools take `page`/`perPage`; cursor-paginated tools take
  `pageSize`/`afterCursor`. The envelope reports `has_more` plus exactly one continuation field:
  `next_page` (a page number) or `after_cursor`. Page-size defaults are explicit per tool (25 for entity
  lists; smaller for heavy rows; never Zendesk's implicit 100).
- **Sideloads** — list/read tools accept `include` (e.g. `users`, `groups`, `organizations`) where the Zendesk
  endpoint supports it, returning sibling arrays that remove per-id follow-up lookups.
- **Errors** — Zendesk failures surface to the agent with the real HTTP status and response body (never the
  SDK's opaque generic error), including `Retry-After` semantics on 429s.
- **Bulk writes** — bulk operations (≤100 items unless noted) return a `job_status`; poll
  `job_statuses_get` until `completed`/`failed`.

### Read tools (100)

| Area | Tools |
| --- | --- |
| Users (15) | `users_me_get`, `users_get`, `users_get_many`, `users_search`, `users_tickets_requested_list`, `users_list`, `users_count`, `users_autocomplete`, `users_related_get`, `users_identities_list`, `users_groups_list`, `users_organizations_list`, `users_tickets_assigned_list`, `users_tickets_ccd_list`, `users_tags_list` |
| Tickets (17) | `tickets_get`, `tickets_search`, `tickets_comments_list`, `tickets_audits_list`, `tickets_metrics_get`, `tickets_metric_events_export`, `tickets_incidents_list`, `tickets_side_conversations_list`, `tickets_list`, `tickets_get_many`, `tickets_count`, `tickets_get_by_external_id`, `tickets_collaborators_list`, `tickets_comments_count`, `tickets_export_incremental`, `tickets_search_export` (cursor-only deep export, no 1k cap), `tickets_deleted_list` (id source for restore/purge) |
| Organizations (13) | `organizations_get`, `organizations_tickets_list`, `organizations_tickets_count`, `organizations_list`, `organizations_count`, `organizations_get_many`, `organizations_get_by_name_or_external_id`, `organizations_autocomplete`, `organizations_users_list`, `organizations_users_count`, `organizations_memberships_list`, `organizations_merges_get`, `organizations_tags_list` |
| Groups (7) | `groups_list`, `groups_get`, `groups_memberships_list`, `groups_assignable_list`, `groups_count`, `groups_users_list`, `groups_users_count` |
| Help Center (8) | `articles_search`, `articles_deflection_search` (Guide's suggested articles for a question), `articles_get`, `articles_list`, `articles_sections_list`, `articles_sections_get`, `articles_categories_list`, `articles_categories_get` |
| Ticket fields (4) | `ticket_fields_list`, `ticket_fields_get`, `ticket_fields_get_many`, `ticket_fields_options_list` |
| Macros (6) | `macros_list`, `macros_get`, `macros_list_active`, `macros_search`, `macros_changes_get` (preview a macro's changes), `macros_ticket_preview_get` (preview a ticket after a macro) |
| Forms (2) | `forms_list`, `forms_get` |
| Views (6) | `views_list`, `views_get`, `views_tickets_list`, `views_rows_list` (run a view as a work queue), `views_count`, `views_count_many` (bulk queue sizes) |
| Search (1) | `search_count` |
| Brands (2) | `brands_list`, `brands_get` |
| Custom statuses (2) | `custom_statuses_list`, `custom_statuses_get` |
| Job statuses (3) | `job_statuses_list`, `job_statuses_get`, `job_statuses_get_many` |
| Tags (3) | `tags_list`, `tags_count`, `tags_autocomplete` |
| Suspended tickets (2) | `suspended_tickets_list`, `suspended_tickets_get` |
| Attachments (1) | `attachments_get` (authenticated content download; text or size-capped base64) |
| Satisfaction ratings (3) | `satisfaction_ratings_list`, `satisfaction_ratings_get`, `satisfaction_ratings_count` (CSAT reads) |
| Community (1) | `community_posts_search` (Gather peer discussions) |
| Custom objects (4) | `custom_objects_list`, `custom_objects_records_list`, `custom_objects_records_search`, `custom_objects_records_get` (tenant business data) |

### Write tools (115)

| Area | Tools |
| --- | --- |
| Tickets (40) | Single-action setters (decomposed for per-action gating): `tickets_reply_public` (public reply), `tickets_note_add` (internal note), `tickets_status_set`, `tickets_priority_set`, `tickets_type_set`, `tickets_assignee_set`, `tickets_group_set`, `tickets_requester_set`, `tickets_organization_set`, `tickets_form_set`, `tickets_custom_fields_set`, `tickets_collaborators_set`, `tickets_due_at_set`, `tickets_subject_set`; bulk (async job): `tickets_status_set_many`, `tickets_assignee_set_many`, `tickets_group_set_many`, `tickets_tags_add_many`, `tickets_tags_remove_many`, `tickets_note_add_many`, `tickets_reply_public_many`, `tickets_custom_fields_set_many`; plus `tickets_create`, `tickets_create_many`, `tickets_delete`, `tickets_delete_many`, `tickets_merge`, `tickets_mark_spam`, `tickets_mark_spam_many`, `tickets_restore`, `tickets_restore_many`, `tickets_delete_permanently`, `tickets_delete_permanently_many`, `tickets_tags_set`, `tickets_tags_add`, `tickets_tags_remove`, `tickets_comments_make_private`, `tickets_comments_attachment_redact`, `tickets_import`, `tickets_import_many` |
| Users (23) | Single-action setters: `users_role_set` (privilege), `users_suspended_set` (access), `users_ticket_restriction_set` (privilege), `users_name_set`, `users_phone_set`, `users_organization_set`, `users_notes_set`, `users_tags_set`, `users_fields_set`; plus `users_create`, `users_create_or_update`, `users_create_many`, `users_create_or_update_many`, `users_merge`, `users_delete`, `users_delete_many`, `users_delete_permanently`, `users_identities_create`, `users_identities_update`, `users_identities_make_primary`, `users_identities_verify`, `users_identities_request_verification`, `users_identities_delete` |
| Organizations (17) | Single-action setters: `organizations_name_set`, `organizations_domains_set`, `organizations_notes_set`, `organizations_tags_set`, `organizations_fields_set`, `organizations_sharing_set`; plus `organizations_create`, `organizations_create_many`, `organizations_create_or_update`, `organizations_delete`, `organizations_delete_many`, `organizations_merge` (poll `organizations_merges_get`), `organizations_memberships_create`, `organizations_memberships_create_many`, `organizations_memberships_delete`, `organizations_memberships_delete_many`, `organizations_memberships_make_default` |
| Groups (8) | `groups_create`, `groups_update`, `groups_delete`, `groups_memberships_create`, `groups_memberships_create_many`, `groups_memberships_delete`, `groups_memberships_delete_many`, `groups_memberships_make_default` |
| Forms (4) | `forms_create`, `forms_update`, `forms_delete`, `forms_clone` |
| Ticket fields (5) | `ticket_fields_create`, `ticket_fields_update`, `ticket_fields_delete`, `ticket_fields_options_create_or_update`, `ticket_fields_options_delete` |
| Macros (3) | `macros_create`, `macros_update`, `macros_delete` |
| Views (3) | `views_create`, `views_update`, `views_delete` |
| Brands (3) | `brands_create`, `brands_update`, `brands_delete` |
| Custom statuses (3) | `custom_statuses_create`, `custom_statuses_update`, `custom_statuses_delete` |
| Suspended tickets (4) | `suspended_tickets_recover`, `suspended_tickets_recover_many`, `suspended_tickets_delete`, `suspended_tickets_delete_many` |
| Uploads (2) | `uploads_create` (base64 content → upload token for ticket comments), `uploads_delete` |

## Configuration

Zendesk config lives under `Ignite:Zendesk` (the Spark convention). All configuration is
environment-overridable (`__` maps to `:`, e.g. `Ignite__Zendesk__OAuth__ClientSecret`).

```jsonc
{
  "Ignite": {
    "Zendesk": {
      "Subdomain": "acme",              // -> https://acme.zendesk.com/api/v2/
      "BaseUrl": null,                  // optional explicit override (sandbox / test)
      "OAuth": {                        // OAuth 2.0 client_credentials (confidential client)
        "ClientId": "***",              // Zendesk OAuth client "Unique Identifier"
        "ClientSecret": "***",
        "Scope": "read"                 // use "read write" to enable the write tools
      },
      "Settings": {
        "HealthChecks": { "Enabled": true },   // live GET /users/me on /health/ready
        "Tracing": { "Enabled": true }         // both Zendesk tracing sources (curated + Kiota adapter)
      }
    }
  },
  "Mcp": {
    "Endpoint": "",                         // route prefix for the MCP endpoints ("" = root)
    "Stateless": true,                      // Streamable HTTP, horizontally scalable
    "AllowedOrigins": [],                   // browser Origins allowed through (empty = reject all; see Security)
    "Execution": {
      "Mode": "Default",                    // Default | DryRun | ReadOnly (gates write tools)
      "AllowHeaderOverride": true,
      "HeaderName": "X-Mcp-Execution-Mode"
    },
    "Tools": {
      "Areas": [],                          // [] = all areas; e.g. ["tickets","users"] registers only those
      "MaxResponseChars": 60000,            // response-size budget per tool response (min 1000)
      "MaxResponseCharsByTool": {}          // per-tool overrides, keyed by tool name (case-insensitive)
    }
  }
}
```

For multiple Zendesk accounts, register additional keyed instances, e.g.
`builder.IgniteZendeskClient(name: "sandbox", serviceKey: "sandbox")` bound at `Ignite:Zendesk:sandbox`,
resolved via `GetRequiredKeyedService<ZendeskSupportApiClient>("sandbox")` (and likewise for
`ZendeskHelpCenterApiClient` / `IRequestAdapter`).

### Authentication (OAuth 2.0 client credentials)

Zendesk is [sunsetting legacy API tokens](https://support.zendesk.com/hc/en-us/articles/10840968198042) (no new
tokens after 2026-10-27; all removed 2027-04-30), so this server authenticates with the **OAuth 2.0
`client_credentials`** grant using a **confidential** OAuth client:

1. In Zendesk **Admin Center → Apps and integrations → APIs → OAuth clients**, add an OAuth client of kind
   **Confidential** (no redirect URL needed for server-to-server). Note its **Unique Identifier** (`ClientId`) and
   generated **Secret** (`ClientSecret`).
2. Create it under a **dedicated, least-privilege admin identity** — the token inherits *that user's* permissions
   (Zendesk has no separate service-account entity for this grant).
3. Set `OAuth:ClientId`, `OAuth:ClientSecret`, and `OAuth:Scope` (default `read`; the write tools need
   `read write`).

The client `POST`s `https://{subdomain}.zendesk.com/oauth/tokens` (or `/oauth/tokens` on the `BaseUrl` host when
overridden; `grant_type=client_credentials`), caches the returned bearer token, refreshes it proactively before
expiry (default ~30 min), and retries once on a `401`.

Provide credentials for local development via `appsettings.Development.json` (git-ignored, excluded from
`dotnet publish` and from the Docker build context) or user secrets:

```bash
dotnet user-secrets set "Ignite:Zendesk:Subdomain" "acme"
dotnet user-secrets set "Ignite:Zendesk:OAuth:ClientId" "***"
dotnet user-secrets set "Ignite:Zendesk:OAuth:ClientSecret" "***"
```

### Execution modes (read-only / dry-run)

The server enforces a baseline **execution mode** and a request may only ever make it _more_ restrictive:

| Mode | Read tools | Write tools |
| --- | --- | --- |
| `Default` | run | perform changes |
| `DryRun` | run | **no changes made** — return an explicit `{"status":"dry_run","executed":false,…}` payload describing what would have happened (verbatim request echo for single-entity writes, a compact digest for bulk `*_many` writes) |
| `ReadOnly` | run | **not registered** (baseline) / **rejected** (per-request header) |

A per-request header (`X-Mcp-Execution-Mode: dry-run` \| `read-only`) can tighten the mode but never relax the
configured baseline — a `ReadOnly` deployment can never be talked into writing. The header check **fails
closed**: a present-but-unrecognized value resolves to `ReadOnly` rather than silently falling back to the
(less restrictive) baseline. Every write tool routes through a single guard
(`ZendeskToolInvoker.InvokeWriteAsync`), so the mode guarantees hold uniformly.

## Security (deployment)

⚠️ **The MCP endpoint is mapped without authentication** (`app.MapMcp(...)` has no `RequireAuthorization()`), so any
caller that can reach it invokes tools using the server's single set of Zendesk credentials. The execution-mode
gate constrains *what* a caller may do (read-only / dry-run) but does **not** authenticate *who* the caller is.
Before exposing this server:

- Bind it to a trusted network only (e.g. a private interface / service mesh), **or**
- Front it with authentication (an API gateway, mTLS, or the MCP SDK's bearer/OAuth resource-server support) and
  add `.RequireAuthorization()` to the mapped endpoint.

**Origin validation (DNS rebinding).** Per the MCP Streamable HTTP transport specification, the host validates
the `Origin` header on incoming requests: requests carrying an `Origin` not present in `Mcp:AllowedOrigins` are
rejected with `403`. The default (empty list) rejects **all** browser origins; non-browser clients (MCP agents,
CLIs) send no `Origin` header and always pass. Add explicit origins only if a browser-based client must connect.

The Ignite health endpoints (`/health/live`, `/health/ready`) are likewise unauthenticated; keep them off any
public interface.

## Run

```bash
dotnet run --project src/ES.FX.Zendesk.MCP.Host        # http://localhost:8080
```

Health endpoints (from Ignite): `/health/live`, `/health/ready` (the latter live-pings Zendesk).

## Docker

```bash
# build from the repository root (build context = repo root)
docker build -f src/ES.FX.Zendesk.MCP.Host/Dockerfile -t es-fx-mcp-zendesk .
docker run --rm -p 8080:8080 \
  -e Ignite__Zendesk__Subdomain=acme \
  -e Ignite__Zendesk__OAuth__ClientId=*** \
  -e Ignite__Zendesk__OAuth__ClientSecret=*** \
  es-fx-mcp-zendesk
```

The image never contains development secrets: `appsettings.Development.json` is excluded from the build context
(`.dockerignore`) *and* from publish output (`CopyToPublishDirectory=Never`). Configuration enters the container
via environment variables or your orchestrator's secret store.

## Observability

Logging via Serilog; OpenTelemetry traces/metrics via Ignite. Both Zendesk tracing sources
(`ES.FX.Zendesk` and the Kiota adapter's `Microsoft.Kiota.Http.HttpClientLibrary`) are wired in by the
Spark; the MCP SDK's `ActivitySource`/`Meter` (`Experimental.ModelContextProtocol`) is wired in by the
host's `AddZendeskMcpServer()`.
