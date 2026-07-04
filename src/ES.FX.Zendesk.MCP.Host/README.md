# ES.FX.Zendesk.MCP.Host

A [Model Context Protocol](https://modelcontextprotocol.io) (MCP) server that exposes Zendesk Support as
namespaced MCP tools — the **full read and write surface** of the `ES.FX.Zendesk` client. Built on
**ES.FX.Ignite** and the official
[`ModelContextProtocol.AspNetCore`](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore) SDK, served
over the Streamable HTTP transport.

> This is a runnable application (not a published NuGet package). It consumes:
> - **`ES.FX.Zendesk`** — the reusable, HttpClientFactory-based Zendesk API client (supports multiple named/keyed instances).
> - **`ES.FX.Ignite.Zendesk`** — the Ignite Spark that binds/wires the client (`builder.IgniteZendeskClient(name?, serviceKey?)`).
>
> The MCP server itself is wired directly in this app (`Hosting/McpServerHostingExtensions.cs`) rather than a
> standalone package, since an MCP server is only meaningful as part of a concrete implementation.

## Tools

**168 tools** namespaced `zendesk_{resource}_{verb}`, mirroring the Zendesk API: **81 read tools**
(`ReadOnly = true`) and **87 write tools** (`ReadOnly = false`, with truthful `Destructive`/`Idempotent`
annotations). Write tools are gated by the [execution mode](#execution-modes-read-only--dry-run) and are **not
registered at all** when the configured baseline is `ReadOnly`, so a read-only deployment advertises a purely
read-only tool list.

Conventions shared by all tools:

- **Pagination** — offset-paginated tools take `page`/`perPage` and return `count` + `next_page` (search-style
  tools default `perPage` to 25); cursor-paginated tools take `pageSize`/`afterCursor` and return
  `meta.has_more` + `meta.after_cursor`. `zendesk_ticket_fields_list` takes no paging parameters and returns
  the full set in one call (a documented Zendesk exception).
- **Sideloads** — list/read tools accept `include` (e.g. `users`, `groups`, `organizations`) where the Zendesk
  endpoint supports it, returning sibling arrays that remove per-id follow-up lookups.
- **Errors** — Zendesk failures surface to the agent with the real HTTP status and response body (never the
  SDK's opaque generic error), including `Retry-After` semantics on 429s.
- **Bulk writes** — bulk operations (≤100 items unless noted) return a `job_status`; poll
  `zendesk_job_statuses_read` until `completed`/`failed`.

### Read tools (81)

| Area | Tools (`zendesk_…`) |
| --- | --- |
| Users (15) | `users_whoami`, `users_read`, `users_read_many`, `users_search`, `users_requested_tickets`, `users_list`, `users_count`, `users_autocomplete`, `users_related`, `users_identities`, `users_groups`, `users_organizations`, `users_assigned_tickets`, `users_ccd_tickets`, `users_tags` |
| Tickets (15) | `tickets_read`, `tickets_search`, `tickets_comments`, `tickets_audits`, `tickets_metrics`, `tickets_metric_events`, `tickets_incidents`, `tickets_side_conversations`, `tickets_list`, `tickets_read_many`, `tickets_count`, `tickets_read_by_external_id`, `tickets_collaborators`, `tickets_comments_count`, `tickets_incremental` |
| Organizations (11) | `organizations_read`, `organizations_tickets`, `organizations_list`, `organizations_count`, `organizations_read_many`, `organizations_search`, `organizations_autocomplete`, `organizations_users`, `organizations_memberships`, `organizations_merge_status`, `organizations_tags` |
| Groups (6) | `groups_list`, `groups_read`, `groups_memberships`, `groups_assignable`, `groups_count`, `groups_users` |
| Help Center (7) | `articles_search`, `articles_read`, `articles_list`, `articles_sections`, `articles_section_read`, `articles_categories`, `articles_category_read` |
| Ticket fields (3) | `ticket_fields_list`, `ticket_fields_read`, `ticket_fields_options` |
| Macros (3) | `macros_list`, `macros_read`, `macros_list_active` |
| Forms (2) | `forms_search`, `forms_read` |
| Views (4) | `views_list`, `views_read`, `views_tickets`, `views_count` |
| Search (2) | `search_count`, `search_export_tickets` (cursor-only deep export, no 1k cap) |
| Brands (2) | `brands_list`, `brands_read` |
| Custom statuses (2) | `custom_statuses_list`, `custom_statuses_read` |
| Job statuses (3) | `job_statuses_list`, `job_statuses_read`, `job_statuses_read_many` |
| Tags (3) | `tags_list`, `tags_count`, `tags_autocomplete` |
| Suspended tickets (2) | `suspended_tickets_list`, `suspended_tickets_read` |
| Attachments (1) | `attachments_read` (authenticated content download; text or size-capped base64) |

### Write tools (87)

| Area | Tools (`zendesk_…`) |
| --- | --- |
| Tickets (21) | `tickets_create`, `tickets_create_many`, `tickets_update` (public reply / internal note via `comment.public`; 409 optimistic locking via `SafeUpdate`/`UpdatedStamp`), `tickets_update_many`, `tickets_update_many_batch`, `tickets_delete`, `tickets_delete_many`, `tickets_merge`, `tickets_mark_spam`, `tickets_mark_spam_many`, `tickets_restore`, `tickets_restore_many`, `tickets_delete_permanently`, `tickets_delete_permanently_many`, `tickets_tags_set`, `tickets_tags_add`, `tickets_tags_remove`, `tickets_comment_make_private`, `tickets_comment_attachment_redact`, `tickets_import`, `tickets_import_many` |
| Users (17) | `users_create`, `users_create_or_update`, `users_create_many`, `users_create_or_update_many`, `users_update`, `users_update_many`, `users_update_many_batch`, `users_merge`, `users_delete`, `users_delete_many`, `users_delete_permanently`, `users_identities_create`, `users_identities_update`, `users_identities_make_primary`, `users_identities_verify`, `users_identities_request_verification`, `users_identities_delete` |
| Organizations (14) | `organizations_create`, `organizations_create_many`, `organizations_create_or_update`, `organizations_update`, `organizations_update_many`, `organizations_update_many_batch`, `organizations_delete`, `organizations_delete_many`, `organizations_merge` (poll `organizations_merge_status`), `organizations_memberships_create`, `organizations_memberships_create_many`, `organizations_memberships_delete`, `organizations_memberships_delete_many`, `organizations_memberships_make_default` |
| Groups (8) | `groups_create`, `groups_update`, `groups_delete`, `groups_memberships_create`, `groups_memberships_create_many`, `groups_memberships_delete`, `groups_memberships_delete_many`, `groups_memberships_make_default` |
| Forms (4) | `forms_create`, `forms_update`, `forms_delete`, `forms_clone` |
| Ticket fields (5) | `ticket_fields_create`, `ticket_fields_update`, `ticket_fields_delete`, `ticket_fields_options_set`, `ticket_fields_options_delete` |
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
        "Tracing": { "Enabled": true }         // ES.FX.Zendesk ActivitySource
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
    }
  }
}
```

For multiple Zendesk accounts, register additional keyed instances, e.g.
`builder.IgniteZendeskClient(name: "sandbox", serviceKey: "sandbox")` bound at `Ignite:Zendesk:sandbox`,
resolved via `GetRequiredKeyedService<IZendeskClient>("sandbox")`.

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
| `DryRun` | run | **no changes made** — return an explicit `{"status":"dry_run","executed":false,…}` payload describing what would have happened, echoing the request |
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
docker build -f src/ES.FX.Zendesk.MCP.Host/Dockerfile -t es-fx-zendesk-mcp .
docker run --rm -p 8080:8080 \
  -e Ignite__Zendesk__Subdomain=acme \
  -e Ignite__Zendesk__OAuth__ClientId=*** \
  -e Ignite__Zendesk__OAuth__ClientSecret=*** \
  es-fx-zendesk-mcp
```

The image never contains development secrets: `appsettings.Development.json` is excluded from the build context
(`.dockerignore`) *and* from publish output (`CopyToPublishDirectory=Never`). Configuration enters the container
via environment variables or your orchestrator's secret store.

## Observability

Logging via Serilog; OpenTelemetry traces/metrics via Ignite. The Zendesk client's `ActivitySource`
(`ES.FX.Zendesk`) is wired in by the Spark; the MCP SDK's `ActivitySource`/`Meter`
(`Experimental.ModelContextProtocol`) is wired in by the host's `AddZendeskMcpServer()`.
