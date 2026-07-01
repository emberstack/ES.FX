# ES.FX.Zendesk.MCP.Host

A [Model Context Protocol](https://modelcontextprotocol.io) (MCP) server that exposes Zendesk Support as
namespaced MCP tools. Built on **ES.FX.Ignite** and the official
[`ModelContextProtocol.AspNetCore`](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore) SDK, served
over the Streamable HTTP transport.

> This is a runnable application (not a published NuGet package). It consumes:
> - **`ES.FX.Zendesk`** — the reusable, HttpClientFactory-based Zendesk API client (supports multiple named/keyed instances).
> - **`ES.FX.Ignite.Zendesk`** — the Ignite Spark that binds/wires the client (`builder.IgniteZendeskClient(name?, serviceKey?)`).
>
> The MCP server itself is wired directly in this app (`Hosting/McpServerHostingExtensions.cs`) rather than a
> standalone package, since an MCP server is only meaningful as part of a concrete implementation.

## Tools

Tools are namespaced `zendesk_{resource}_{verb}` to mirror the Zendesk API.

All implemented tools are **read-only** (`ReadOnly = true, OpenWorld = true`). Search/list tools default `perPage`
to 25 (100 for `ticket_fields_list`, `groups_list`, `groups_memberships`) and expose `count` + `next_page` for
pagination.

A read ticket carries requester/assignee/group/organization ids, tags, collaborators/CCs, **custom field values**,
satisfaction rating, and problem/incident links. Resolve the people ids with `zendesk_users_read_many`, the group id
with `zendesk_groups_read`, and decode custom fields with `zendesk_ticket_fields_list`. To avoid per-id round-trips,
`zendesk_tickets_search`, `zendesk_organizations_tickets`, and `zendesk_users_requested_tickets` accept an
`include` sideload (`users`, `groups`, `organizations`) returned as sibling arrays.

**Users & groups**
| Tool | Zendesk endpoint |
| --- | --- |
| `zendesk_users_whoami` | `GET /api/v2/users/me.json` |
| `zendesk_users_read` | `GET /api/v2/users/{id}.json` |
| `zendesk_users_read_many` | `GET /api/v2/users/show_many.json?ids=` (batch id→name, ≤100) |
| `zendesk_users_search` | `GET /api/v2/users/search.json` |
| `zendesk_users_requested_tickets` | `GET /api/v2/users/{id}/tickets/requested.json` |
| `zendesk_groups_list` | `GET /api/v2/groups.json` (resolve a `group_id` to a name) |
| `zendesk_groups_read` | `GET /api/v2/groups/{id}.json` |
| `zendesk_groups_memberships` | `GET /api/v2/groups/{id}/memberships.json` (agents in a group) |

**Tickets**
| Tool | Zendesk endpoint |
| --- | --- |
| `zendesk_tickets_read` | `GET /api/v2/tickets/{id}.json` (custom fields, CCs, satisfaction, problem/incident links) |
| `zendesk_tickets_search` | `GET /api/v2/search.json?query=type:ticket ...` (`include=tickets(users,groups,organizations)`) |
| `zendesk_tickets_comments` | `GET /api/v2/tickets/{id}/comments.json` (`bodyFormat` = plain \| rich \| both) |
| `zendesk_tickets_audits` | `GET /api/v2/tickets/{id}/audits.json` (change history) |
| `zendesk_tickets_metrics` | `GET /api/v2/tickets/{id}/metrics.json` (aggregate timing / reopens) |
| `zendesk_tickets_metric_events` | `GET /api/v2/incremental/ticket_metric_events.json?start_time=` (SLA breach timeline export; filter by `ticket_id` — Zendesk has no per-ticket variant) |
| `zendesk_tickets_incidents` | `GET /api/v2/tickets/{id}/incidents.json` (blast radius of a problem) |
| `zendesk_tickets_side_conversations` | `GET /api/v2/tickets/{id}/side_conversations.json` (vendor/escalation threads; add-on) |
| `zendesk_attachments_read` | `GET /api/v2/attachments/{id}.json` → authenticated content (text or size-capped base64) |

**Organizations, Help Center, schema & macros**
| Tool | Zendesk endpoint |
| --- | --- |
| `zendesk_organizations_read` | `GET /api/v2/organizations/{id}.json` |
| `zendesk_organizations_tickets` | `GET /api/v2/organizations/{id}/tickets.json` |
| `zendesk_articles_search` | `GET /api/v2/help_center/articles/search.json` (knowledge base) |
| `zendesk_articles_read` | `GET /api/v2/help_center/articles/{id}.json` |
| `zendesk_ticket_fields_list` | `GET /api/v2/ticket_fields.json` (decode custom fields) |
| `zendesk_ticket_fields_read` | `GET /api/v2/ticket_fields/{id}.json` |
| `zendesk_macros_list` | `GET /api/v2/macros.json` (canned responses) |
| `zendesk_macros_read` | `GET /api/v2/macros/{id}.json` |
| `zendesk_forms_search` | `GET /api/v2/ticket_forms.json` (lists all forms) |
| `zendesk_forms_read` | `GET /api/v2/ticket_forms/{id}.json` |

**Planned (write)** — not built: `zendesk_tickets_write`, `zendesk_tickets_reply_public`,
`zendesk_tickets_reply_internal` (will be `readOnlyHint:false`, gated by execution mode, and require OAuth `write` scope).

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
        "Scope": "read"                 // space-separated; "read write" / resource scopes for writes
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
3. Set `OAuth:ClientId`, `OAuth:ClientSecret`, and `OAuth:Scope` (default `read`).

The client `POST`s `https://{subdomain}.zendesk.com/oauth/tokens` (or `/oauth/tokens` on the `BaseUrl` host when
overridden; `grant_type=client_credentials`), caches the returned bearer token, refreshes it proactively before
expiry (default ~30 min), and retries once on a `401`.

Provide credentials for local development via `appsettings.Development.json` (git-ignored) or user secrets:

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
| `DryRun` | run | accepted, **no changes made** |
| `ReadOnly` | run | **rejected** |

A per-request header (`X-Mcp-Execution-Mode: dry-run` \| `read-only`) can tighten the mode but never relax the
configured baseline — a `ReadOnly` deployment can never be talked into writing.

## Security (deployment)

⚠️ **The MCP endpoint is mapped without authentication** (`app.MapMcp(...)` has no `RequireAuthorization()`), so any
caller that can reach it invokes tools using the server's single set of Zendesk credentials. The execution-mode
gate constrains *what* a caller may do (read-only / dry-run) but does **not** authenticate *who* the caller is.
Before exposing this server:

- Bind it to a trusted network only (e.g. a private interface / service mesh), **or**
- Front it with authentication (an API gateway, mTLS, or the MCP SDK's bearer/OAuth resource-server support) and
  add `.RequireAuthorization()` to the mapped endpoint.

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
docker build -f src/ES.FX.Zendesk.MCP.Host/Dockerfile -t es-mcp-zendesk .
docker run --rm -p 8080:8080 \
  -e Ignite__Zendesk__Subdomain=acme \
  -e Ignite__Zendesk__OAuth__ClientId=*** \
  -e Ignite__Zendesk__OAuth__ClientSecret=*** \
  es-mcp-zendesk
```

## Observability

Logging via Serilog; OpenTelemetry traces/metrics via Ignite. The Zendesk client's `ActivitySource`
(`ES.FX.Zendesk`) is wired in by the Spark; the MCP SDK's `ActivitySource`/`Meter`
(`Experimental.ModelContextProtocol`) is wired in by the host's `AddZendeskMcpServer()`.
