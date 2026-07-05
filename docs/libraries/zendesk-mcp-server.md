---
title: Zendesk MCP server
description: A deployable Model Context Protocol server exposing the full ES.FX.Zendesk client surface — 168 read and write tools — over Streamable HTTP, with execution-mode gating and Origin validation.
---

## Overview

`ES.FX.Zendesk.MCP.Host` is a runnable [Model Context Protocol](https://modelcontextprotocol.io) (MCP)
server that exposes Zendesk Support as MCP tools an AI agent can call. It wraps the
[ES.FX.Zendesk typed client](./zendesk-client.md) and publishes **168 tools** — **81 read** and
**87 write** (32 of the writes destructive) — that map one-to-one onto the client's operations across all
seventeen resource areas.

Unlike the rest of `ES.FX.*`, this is a **deployable application, not a NuGet package** (it sets
`IsPackable=false` / `GeneratePackageOnBuild=false`). It is built on [Ignite](../ignite/index.md) and the
official [`ModelContextProtocol.AspNetCore`](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore)
SDK, and is served over the Streamable HTTP transport.

The Zendesk vertical spans three pieces; this page covers the third:

| Piece | Package / app | Page |
| --- | --- | --- |
| Typed API client | `ES.FX.Zendesk` | [Zendesk API client](./zendesk-client.md) |
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
    builder.IgniteZendeskClient();    // the Zendesk Spark: IZendeskClient + live health check + tracing

    builder.AddZendeskMcpServer()     // MCP server (Streamable HTTP) + execution-mode services
        .WithTools<ZendeskUserTools>()
        // … 16 read tool classes always; 12 write tool classes only when the baseline allows writes
        ;

    var app = builder.Build();
    app.Ignite();
    app.MapZendeskMcp();              // Origin-validation middleware + MapMcp
    await app.RunAsync();
    return 0;
});
```

Each tool is a method on a `[McpServerToolType]` class (one class per resource area, read and write split
into separate classes). Every client call is routed through a single `ZendeskToolInvoker` so error handling
and execution-mode gating are uniform (see [Error handling](#error-handling) and
[Execution modes](#execution-modes)).

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
| `DryRun` | run | **not executed** — return an explicit `{ "status": "dry_run", "executed": false, … }` payload describing the change that *would* have been made, echoing the request |
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
    }
  }
}
```

The OAuth model, keyed multi-tenant instances, and secret hygiene are covered on the
[Zendesk API client](./zendesk-client.md#authentication) and [Zendesk Spark](../ignite/sparks/zendesk.md)
pages. **Write tools require the OAuth scope to include `write`** (`"read write"`).

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
generic error. `ZendeskToolInvoker` catches the client's
[`ZendeskApiException`](./zendesk-client.md#error-handling) and rethrows it as an `McpException` whose message
the SDK surfaces verbatim, so an agent can distinguish `404` from `403` from `422` and self-correct (and
observe `Retry-After` on `429`).

## Tools

168 tools, namespaced `zendesk_{resource}_{verb}` to mirror the Zendesk API. Conventions shared by all tools:

- **Pagination** — offset-paginated tools take `page`/`perPage` and return `count` + `next_page`;
  cursor-paginated tools take `pageSize`/`afterCursor` and return `meta.has_more` + `meta.after_cursor`.
- **Sideloads** — list/read tools accept an `include` parameter where the Zendesk endpoint supports it,
  returning related records as sibling arrays to avoid per-id follow-up calls.
- **Bulk writes** — bulk operations (≤100 items unless noted) return a `job_status`; poll
  `zendesk_job_statuses_read` until it reports `completed`/`failed`.

### Read tools (81 total)

Every read tool is annotated `ReadOnly = true` and is available in all execution modes.

#### Users

| Tool | What it does |
| --- | --- |
| `zendesk_users_whoami` | Returns the Zendesk user associated with the configured credentials. |
| `zendesk_users_read` | Returns a Zendesk user by id. |
| `zendesk_users_search` | Searches Zendesk users. |
| `zendesk_users_read_many` | Returns many users by id in one call (batch resolution). |
| `zendesk_users_requested_tickets` | Returns the tickets a user has requested (their ticket history). |
| `zendesk_users_list` | Lists Zendesk users, optionally filtered by role. |
| `zendesk_users_count` | Returns the (cached, approximate) user count, optionally filtered by role. |
| `zendesk_users_autocomplete` | Suggests users whose name or e-mail starts with a prefix. |
| `zendesk_users_related` | Returns a user's related ticket/subscription counts. |
| `zendesk_users_identities` | Lists a user's identities (e-mails, phone numbers, social handles). |
| `zendesk_users_groups` | Lists the groups an agent belongs to. |
| `zendesk_users_organizations` | Lists the organizations a user belongs to. |
| `zendesk_users_assigned_tickets` | Returns the tickets assigned to an agent. |
| `zendesk_users_ccd_tickets` | Returns the tickets a user is CC'd on. |
| `zendesk_users_tags` | Lists a user's tags. |

#### Tickets

| Tool | What it does |
| --- | --- |
| `zendesk_tickets_read` | Returns a Zendesk ticket by id. |
| `zendesk_tickets_search` | Searches Zendesk tickets. |
| `zendesk_tickets_comments` | Returns the conversation thread (comments) on a ticket. |
| `zendesk_tickets_audits` | Returns a ticket's change history (audits/events). |
| `zendesk_tickets_metrics` | Returns timing/lifecycle metrics for a ticket. |
| `zendesk_tickets_incidents` | Returns the incidents linked to a problem ticket. |
| `zendesk_tickets_side_conversations` | Returns a ticket's side conversations (vendor/escalation threads). |
| `zendesk_tickets_metric_events` | Exports SLA/metric events across tickets (breach timeline). |
| `zendesk_tickets_list` | Lists tickets. |
| `zendesk_tickets_read_many` | Returns many tickets by id in one call. |
| `zendesk_tickets_count` | Returns the account's total ticket count. |
| `zendesk_tickets_read_by_external_id` | Returns the tickets carrying an external id. |
| `zendesk_tickets_collaborators` | Lists the collaborators (CCs) of a ticket. |
| `zendesk_tickets_comments_count` | Returns a ticket's comment count. |
| `zendesk_tickets_incremental` | Exports tickets incrementally (cursor-based incremental export). |

#### Organizations

| Tool | What it does |
| --- | --- |
| `zendesk_organizations_read` | Returns a Zendesk organization by id. |
| `zendesk_organizations_tickets` | Returns the tickets belonging to an organization. |
| `zendesk_organizations_list` | Lists Zendesk organizations. |
| `zendesk_organizations_count` | Returns the approximate organization count. |
| `zendesk_organizations_read_many` | Returns many Zendesk organizations by id. |
| `zendesk_organizations_search` | Looks up organizations by exact name or external id. |
| `zendesk_organizations_autocomplete` | Suggests organizations whose name starts with a prefix. |
| `zendesk_organizations_users` | Lists the users of an organization. |
| `zendesk_organizations_memberships` | Lists an organization's memberships. |
| `zendesk_organizations_merge_status` | Returns an organization merge job's status. |
| `zendesk_organizations_tags` | Lists an organization's tags. |

#### Groups

| Tool | What it does |
| --- | --- |
| `zendesk_groups_list` | Lists Zendesk groups. |
| `zendesk_groups_read` | Returns a Zendesk group by id. |
| `zendesk_groups_memberships` | Lists the agents that belong to a group. |
| `zendesk_groups_assignable` | Lists the groups assignable to tickets for the current agent. |
| `zendesk_groups_count` | Returns the approximate group count. |
| `zendesk_groups_users` | Lists the users of a group. |

#### Help Center (articles / sections / categories)

| Tool | What it does |
| --- | --- |
| `zendesk_articles_search` | Full-text searches Help Center knowledge base articles. |
| `zendesk_articles_read` | Returns a single Help Center article including its full body. |
| `zendesk_articles_list` | Lists Help Center articles, optionally scoped to a section. |
| `zendesk_articles_sections` | Lists Help Center sections, optionally scoped to a category. |
| `zendesk_articles_section_read` | Returns a single Help Center section by id. |
| `zendesk_articles_categories` | Lists Help Center categories. |
| `zendesk_articles_category_read` | Returns a single Help Center category by id. |

#### Ticket fields

| Tool | What it does |
| --- | --- |
| `zendesk_ticket_fields_list` | Lists ticket field definitions. |
| `zendesk_ticket_fields_read` | Returns a single ticket field definition by id. |
| `zendesk_ticket_fields_options` | Lists the custom options of a drop-down ticket field. |

#### Macros

| Tool | What it does |
| --- | --- |
| `zendesk_macros_list` | Lists Zendesk macros. |
| `zendesk_macros_list_active` | Lists only the macros usable by the current agent. |
| `zendesk_macros_read` | Returns a single macro including its actions. |

#### Ticket forms

| Tool | What it does |
| --- | --- |
| `zendesk_forms_search` | Lists Zendesk ticket forms. |
| `zendesk_forms_read` | Returns a Zendesk ticket form by id. |

#### Views

| Tool | What it does |
| --- | --- |
| `zendesk_views_list` | Lists Zendesk views. |
| `zendesk_views_read` | Returns a Zendesk view by id. |
| `zendesk_views_tickets` | Returns the tickets currently matching a view. |
| `zendesk_views_count` | Returns the (cached) ticket count of a view. |

#### Search

| Tool | What it does |
| --- | --- |
| `zendesk_search_count` | Returns the number of results a search query matches. |
| `zendesk_search_export_tickets` | Exports ticket search results with cursor pagination (no 1,000-result cap). |

#### Brands

| Tool | What it does |
| --- | --- |
| `zendesk_brands_list` | Lists Zendesk brands. |
| `zendesk_brands_read` | Returns a Zendesk brand by id. |

#### Custom statuses

| Tool | What it does |
| --- | --- |
| `zendesk_custom_statuses_list` | Lists Zendesk custom ticket statuses. |
| `zendesk_custom_statuses_read` | Returns a Zendesk custom ticket status by id. |

#### Job statuses

| Tool | What it does |
| --- | --- |
| `zendesk_job_statuses_list` | Lists recent Zendesk job statuses. |
| `zendesk_job_statuses_read` | Returns a Zendesk job status by id. |
| `zendesk_job_statuses_read_many` | Returns many Zendesk job statuses in one request. |

#### Tags

| Tool | What it does |
| --- | --- |
| `zendesk_tags_list` | Lists the most popular Zendesk tags with usage counts. |
| `zendesk_tags_count` | Returns the account-wide tag count. |
| `zendesk_tags_autocomplete` | Suggests Zendesk tag names matching a prefix. |

#### Suspended tickets

| Tool | What it does |
| --- | --- |
| `zendesk_suspended_tickets_list` | Lists Zendesk suspended tickets. |
| `zendesk_suspended_tickets_read` | Returns a Zendesk suspended ticket by id. |

#### Attachments

| Tool | What it does |
| --- | --- |
| `zendesk_attachments_read` | Downloads an attachment's content (text inline, binary as size-capped base64). |

### Write tools (87 total)

Write tools are annotated `ReadOnly = false` and are gated by the [execution mode](#execution-modes):
rejected under `ReadOnly`, simulated under `DryRun`. Rows marked **destructive** carry `Destructive = true`
(they delete, purge, merge, redact, or otherwise irreversibly alter data). Bulk tools noted as "async job"
return a `job_status` — poll `zendesk_job_statuses_read`.

#### Tickets

| Tool | Type | What it does |
| --- | --- | --- |
| `zendesk_tickets_create` | write | Creates a Zendesk ticket. |
| `zendesk_tickets_create_many` | write | Creates up to 100 Zendesk tickets as an async job. |
| `zendesk_tickets_update` | write | Updates a Zendesk ticket by id (public reply / internal note via `comment.public`). |
| `zendesk_tickets_update_many` | write | Applies the same change to up to 100 tickets as an async job. |
| `zendesk_tickets_update_many_batch` | write | Applies per-ticket changes to up to 100 tickets as an async job. |
| `zendesk_tickets_delete` | **destructive** | Soft-deletes a Zendesk ticket. |
| `zendesk_tickets_delete_many` | **destructive** | Soft-deletes up to 100 tickets as an async job. |
| `zendesk_tickets_merge` | **destructive** | Merges source tickets into a target ticket as an async job. |
| `zendesk_tickets_mark_spam` | **destructive** | Marks a ticket as spam and suspends its requester. |
| `zendesk_tickets_mark_spam_many` | **destructive** | Marks up to 100 tickets as spam as an async job. |
| `zendesk_tickets_restore` | write | Restores a soft-deleted ticket. |
| `zendesk_tickets_restore_many` | write | Restores up to 100 soft-deleted tickets. |
| `zendesk_tickets_delete_permanently` | **destructive** | Permanently deletes an already soft-deleted ticket (irreversible). |
| `zendesk_tickets_delete_permanently_many` | **destructive** | Permanently deletes up to 100 soft-deleted tickets as an async job (irreversible). |
| `zendesk_tickets_tags_set` | write | Replaces a ticket's whole tag set. |
| `zendesk_tickets_tags_add` | write | Adds tags to a ticket without removing existing ones. |
| `zendesk_tickets_tags_remove` | write | Removes tags from a ticket. |
| `zendesk_tickets_comment_make_private` | **destructive** | Makes a public ticket comment private (one-way). |
| `zendesk_tickets_comment_attachment_redact` | **destructive** | Permanently redacts a comment attachment (irreversible). |
| `zendesk_tickets_import` | write | Imports a historical ticket (admin-only; no triggers/notifications). |
| `zendesk_tickets_import_many` | write | Imports up to 100 historical tickets as an async job (admin-only). |

#### Users

| Tool | Type | What it does |
| --- | --- | --- |
| `zendesk_users_create` | write | Creates a Zendesk user. |
| `zendesk_users_create_or_update` | write | Creates or updates a Zendesk user matched by e-mail or external id (upsert). |
| `zendesk_users_create_many` | write | Creates up to 100 Zendesk users as an async job. |
| `zendesk_users_create_or_update_many` | write | Creates or updates up to 100 Zendesk users as an async job. |
| `zendesk_users_update` | write | Updates a Zendesk user by id. |
| `zendesk_users_update_many` | write | Applies the same change to up to 100 Zendesk users as an async job. |
| `zendesk_users_update_many_batch` | write | Applies per-user changes to up to 100 Zendesk users as an async job. |
| `zendesk_users_merge` | **destructive** | Merges one end user into another; the loser is absorbed and the winner survives. |
| `zendesk_users_delete` | **destructive** | Soft-deletes a Zendesk user. |
| `zendesk_users_delete_many` | **destructive** | Soft-deletes up to 100 Zendesk users as an async job. |
| `zendesk_users_delete_permanently` | **destructive** | Permanently deletes an already soft-deleted Zendesk user (irreversible). |
| `zendesk_users_identities_create` | write | Adds an identity (e-mail, phone, social handle) to a Zendesk user. |
| `zendesk_users_identities_update` | write | Updates a Zendesk user identity's value or verification state. |
| `zendesk_users_identities_make_primary` | write | Makes an identity the user's primary identity. |
| `zendesk_users_identities_verify` | write | Marks a Zendesk user identity as verified. |
| `zendesk_users_identities_request_verification` | write | Sends a verification e-mail for a Zendesk user identity. |
| `zendesk_users_identities_delete` | **destructive** | Deletes a Zendesk user identity. |

#### Organizations

| Tool | Type | What it does |
| --- | --- | --- |
| `zendesk_organizations_create` | write | Creates a Zendesk organization. |
| `zendesk_organizations_create_many` | write | Creates up to 100 Zendesk organizations as an async job. |
| `zendesk_organizations_create_or_update` | write | Creates or updates a Zendesk organization, matching by id or external id. |
| `zendesk_organizations_update` | write | Updates a Zendesk organization by id. |
| `zendesk_organizations_update_many` | write | Applies the same change to up to 100 organizations as an async job. |
| `zendesk_organizations_update_many_batch` | write | Applies per-organization changes to up to 100 organizations as an async job. |
| `zendesk_organizations_delete` | **destructive** | Deletes a Zendesk organization by id (permanent; no restore). |
| `zendesk_organizations_delete_many` | **destructive** | Deletes up to 100 Zendesk organizations as an async job (permanent). |
| `zendesk_organizations_merge` | **destructive** | Merges one Zendesk organization into another (irreversible). |
| `zendesk_organizations_memberships_create` | write | Links a user to an organization. |
| `zendesk_organizations_memberships_create_many` | write | Creates up to 100 organization memberships as an async job. |
| `zendesk_organizations_memberships_delete` | **destructive** | Removes an organization membership by its membership id. |
| `zendesk_organizations_memberships_delete_many` | **destructive** | Removes up to 100 organization memberships as an async job. |
| `zendesk_organizations_memberships_make_default` | write | Makes an organization membership the user's default. |

#### Groups

| Tool | Type | What it does |
| --- | --- | --- |
| `zendesk_groups_create` | write | Creates a Zendesk group. |
| `zendesk_groups_update` | write | Updates a Zendesk group by id. |
| `zendesk_groups_delete` | **destructive** | Soft-deletes a Zendesk group by id. |
| `zendesk_groups_memberships_create` | write | Assigns an agent to a group. |
| `zendesk_groups_memberships_create_many` | write | Assigns up to 100 agents to groups as an async job. |
| `zendesk_groups_memberships_delete` | **destructive** | Removes a group membership by its membership id. |
| `zendesk_groups_memberships_delete_many` | **destructive** | Removes up to 100 group memberships as an async job. |
| `zendesk_groups_memberships_make_default` | write | Makes a group membership the agent's default. |

#### Ticket forms

| Tool | Type | What it does |
| --- | --- | --- |
| `zendesk_forms_create` | write | Creates a Zendesk ticket form. |
| `zendesk_forms_update` | write | Updates a Zendesk ticket form. |
| `zendesk_forms_delete` | **destructive** | Deletes a Zendesk ticket form. |
| `zendesk_forms_clone` | write | Clones a Zendesk ticket form. |

#### Ticket fields

| Tool | Type | What it does |
| --- | --- | --- |
| `zendesk_ticket_fields_create` | write | Creates a Zendesk ticket field. |
| `zendesk_ticket_fields_update` | write | Updates a Zendesk ticket field. |
| `zendesk_ticket_fields_delete` | **destructive** | Deletes a Zendesk ticket field (irreversible; strips its values from every ticket). |
| `zendesk_ticket_fields_options_set` | write | Creates or updates a single custom field option on a drop-down ticket field. |
| `zendesk_ticket_fields_options_delete` | **destructive** | Deletes a custom field option from a drop-down ticket field. |

#### Macros

| Tool | Type | What it does |
| --- | --- | --- |
| `zendesk_macros_create` | write | Creates a Zendesk macro. |
| `zendesk_macros_update` | write | Updates a Zendesk macro. |
| `zendesk_macros_delete` | **destructive** | Deletes a Zendesk macro. |

#### Views

| Tool | Type | What it does |
| --- | --- | --- |
| `zendesk_views_create` | write | Creates a Zendesk view. |
| `zendesk_views_update` | write | Updates a Zendesk view by id. |
| `zendesk_views_delete` | **destructive** | Deletes a Zendesk view by id. |

#### Brands

| Tool | Type | What it does |
| --- | --- | --- |
| `zendesk_brands_create` | write | Creates a Zendesk brand. |
| `zendesk_brands_update` | write | Updates a Zendesk brand by id. |
| `zendesk_brands_delete` | **destructive** | Soft-deletes a Zendesk brand by id. |

#### Custom statuses

| Tool | Type | What it does |
| --- | --- | --- |
| `zendesk_custom_statuses_create` | write | Creates a Zendesk custom ticket status. |
| `zendesk_custom_statuses_update` | write | Updates a Zendesk custom ticket status by id. |
| `zendesk_custom_statuses_delete` | **destructive** | Deletes a Zendesk custom ticket status by id. |

#### Suspended tickets

| Tool | Type | What it does |
| --- | --- | --- |
| `zendesk_suspended_tickets_recover` | write | Recovers a suspended ticket into a real ticket. |
| `zendesk_suspended_tickets_recover_many` | write | Recovers up to 100 suspended tickets, preserving their original requesters. |
| `zendesk_suspended_tickets_delete` | **destructive** | Deletes a suspended ticket by id. |
| `zendesk_suspended_tickets_delete_many` | **destructive** | Deletes up to 100 suspended tickets. |

#### Uploads

| Tool | Type | What it does |
| --- | --- | --- |
| `zendesk_uploads_create` | write | Uploads a file (base64 content) and returns a token for attaching to a ticket comment. |
| `zendesk_uploads_delete` | **destructive** | Deletes an unconsumed upload by its token. |

## Run

```bash
dotnet run --project src/ES.FX.Zendesk.MCP.Host        # http://localhost:8080
```

Health endpoints (from Ignite): `/health/live`, `/health/ready` (the latter live-pings Zendesk).

### Docker

```bash
# build from the repository root (build context = repo root)
docker build -f src/ES.FX.Zendesk.MCP.Host/Dockerfile -t es-fx-zendesk-mcp .
docker run --rm -p 8080:8080 \
  -e Ignite__Zendesk__Subdomain=acme \
  -e Ignite__Zendesk__OAuth__ClientId=*** \
  -e Ignite__Zendesk__OAuth__ClientSecret=*** \
  es-fx-zendesk-mcp
```

The image never contains development secrets: `appsettings.Development.json` is excluded from both the
build context (`.dockerignore`) and publish output (`CopyToPublishDirectory=Never`). Supply configuration
via environment variables or your orchestrator's secret store.

## Observability

Logging is via Serilog; OpenTelemetry traces and metrics are wired by [Ignite](../ignite/index.md). The
Zendesk client's `ActivitySource` (`ES.FX.Zendesk`) is registered by the Spark; the MCP SDK's
`ActivitySource`/`Meter` (`Experimental.ModelContextProtocol`) is registered by `AddZendeskMcpServer()`.

## See also

- [Zendesk API client](./zendesk-client.md) — the underlying typed client, OAuth model, and error handling.
- [Zendesk Spark](../ignite/sparks/zendesk.md) — the Ignite integration this host builds on.
- [Application hosting](../development/hosting.md) — the `ProgramEntry` lifecycle wrapper used by the host.
- [Framework libraries](./index.md)
- [Model Context Protocol](https://modelcontextprotocol.io) — the protocol specification.
- [Zendesk API reference](https://developer.zendesk.com/api-reference/)
