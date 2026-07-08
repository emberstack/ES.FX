# Zendesk client generation pipeline

The clients under `src/ES.FX.Zendesk/Generated/` are produced by
[Kiota](https://learn.microsoft.com/en-us/openapi/kiota/) (pinned in
[`.config/dotnet-tools.json`](../../.config/dotnet-tools.json)) from Zendesk's official OpenAPI specs.
The generated output is **git-ignored** and **regenerated at build time** — only the committed spec
snapshots, this pipeline, and `Generated/.editorconfig` (analyzer suppression) live in source control.
**Never hand-edit generated files** — they are recreated on the next build. Fixes belong either in this
pipeline (spec patches) or in the curated (non-generated) part of `ES.FX.Zendesk`.

| File | Purpose |
| --- | --- |
| `support-oas.yaml` | Committed snapshot of the [Support API spec](https://developer.zendesk.com/zendesk/oas.yaml) |
| `helpcenter-oas.yaml` | Committed snapshot of the [Help Center API spec](https://developer.zendesk.com/help_center/oas.yaml) |
| `normalize.ps1` | Applies the recorded spec patches below → `*.normalized.yaml` (git-ignored) |
| `generate.ps1` | `normalize.ps1` + `dotnet kiota generate` for both clients, then the post-generation factory repair (git-ignored `Generated/**`) |

## Build-time generation (how it works)

`ES.FX.Zendesk.csproj` regenerates the clients as part of `dotnet build`:

- **`GenerateZendeskClients`** runs `generate.ps1` before compile, but **incrementally** — only when a
  committed input (`support-oas.yaml`, `helpcenter-oas.yaml`, `normalize.ps1`, `generate.ps1`,
  `.config/dotnet-tools.json`) is newer than `Generated/.generated.stamp`. The stamp lives in the
  git-ignored `Generated/` folder, so it **survives `dotnet clean`** and regeneration happens only on a
  real input change or a fresh checkout — not on every build.
- **`IncludeZendeskGeneratedClients`** feeds the generated sources to the compiler on every build,
  hidden from Solution Explorer (`Visible="false"`). (The default SDK glob is told to ignore
  `Generated/**` so the files neither double-compile nor show in the IDE.)
- Generation is **skipped during IDE design-time builds** (`DesignTimeBuild=true`) so IntelliSense never
  shells out to the generator. After one real build the files exist on disk and the IDE resolves them.

**Prerequisites for a build:** the `kiota` local tool (`dotnet tool restore`) and **PowerShell 7
(`pwsh`)** on PATH — `normalize.ps1` uses APIs not present in Windows PowerShell 5.1. Both are present on
GitHub `ubuntu-latest` runners; the CI `build` job runs `dotnet tool restore` before `dotnet build`, and
`src/**` (which includes this pipeline) + `.config/dotnet-tools.json` are in the `paths-filter` so a
spec/generator bump triggers a build.

## Regenerating by hand

Usually unnecessary (the build does it), but to force it — e.g. after editing a patch or to refresh the
snapshots:

```powershell
./src/ES.FX.Zendesk/OpenApi/generate.ps1            # from the committed snapshots
./src/ES.FX.Zendesk/OpenApi/generate.ps1 -Refresh   # re-download the latest specs first
```

Then `dotnet build` && `dotnet test`. Each output folder also contains a `kiota-lock.json` recording the
exact generator version and options used (git-ignored along with the rest of the output).

> **Why git-ignore instead of commit?** The generated tree is ~147k LOC / 8.6 MB (≈88% of `src/` C#),
> which drowned diffs and PRs. The trade-off is real: Kiota's output is not perfectly deterministic
> (see below), so a fresh-checkout or spec-change regeneration can occasionally land on a different
> variant. `generate.ps1` forces single-threaded generation
> (`KIOTA_GENERATION_MAXDEGREEOFPARALLELISM=1`) to remove the structural variance (dropped types) that
> is most likely to break compilation, and the incremental stamp keeps regeneration rare.

## Recorded spec patches

Zendesk's published specs contain constructs that break Kiota or diverge from live-verified API
behavior. `normalize.ps1` records each fix as an asserted patch: if Zendesk changes the underlying
schema, the script **fails loudly** so a human re-validates the patch instead of silently generating a
wrong client.

| # | Target | Problem | Fix |
| --- | --- | --- | --- |
| P1 | Support `UserObject` | `anyOf [UserForAdmin, UserForEndUser]`, no discriminator — fatal Kiota error | Collapse to `allOf [UserForAdmin]` (the superset view) |
| P2 | Support `TicketsUpdateRequest` | Discriminator-less `oneOf` of `{ticket}` (single) / `{tickets}` (batch) — Kiota warns of serialization failure | Merge into one object with both optional properties (names are disjoint; wire-identical for well-formed requests) |
| P3 | Support `UserInput` | Discriminator-less `anyOf [UserCreateInput, UserMergeInput]` | `allOf` merge (consistent property types; merge variant only adds id/email/external_id identification) |
| P4 | Support `JobStatusResultObject` | Discriminator-less `oneOf` of Create/Update/Failed result shapes — undeserializable | Flatten to the documented union of the three (nothing invented) |
| P5 | Help Center paths | Spec omits the `.json` suffix, but live testing produced HTTP **415** on extension-less Help Center paths | Append `.json` to every path key (the form the previous hand-written client used in production) |

## Known generator hazards

- **Generated models are LOSSY on re-serialization — never round-trip a response through them.** The
  spec marks server-assigned fields (`id`, `created_at`, `url`, counts, `next_page`, …) `readOnly`, and
  Kiota's generated `Serialize()` skips read-only properties entirely (e.g. `GroupObject.Serialize`
  writes only `description`/`is_public`/`name` — no `id`). Deserialize-then-reserialize therefore
  silently strips the most important fields. Consumers that need the response JSON (e.g. the MCP host
  tools) must read the raw response body (send the builder-produced `RequestInformation` and parse the
  stream) instead of serializing the typed model back out. Typed models remain fine for *requests* and
  for *reading* fields in code.
- **Kiota 1.32.5 generation is nondeterministic** (measured 2026-07-05: 5 runs of the identical
  normalized Support spec produced 2–3 distinct outputs). Two failure modes were observed: (a) under
  default parallelism, a run occasionally **drops a type** (e.g. `TicketCreateVoicemailTicketVoiceCommentInput`);
  (b) the base class of a derived (`allOf`) model can flip (e.g. `ItamAssetField` / `CustomObjectField`
  vs. their base `CustomFieldObject`), so a property/collection deserializer emits the **base** type's
  `CreateFromDiscriminatorValue` while the generic argument is the **derived** type — a **CS0407**
  ("wrong return type") compile error. Two guards make generation reliable: `generate.ps1` forces
  `KIOTA_GENERATION_MAXDEGREEOFPARALLELISM=1`, which eliminated the structural (dropped-type) variance
  (a) in testing; and a **post-generation factory repair** (also in `generate.ps1`) rewrites every
  `GetObjectValue<T>(F.CreateFromDiscriminatorValue)` / `GetCollectionOfObjectValues<T>(F.…)` to force
  `F := T`, deterministically fixing the base-class flip (b) — correct Kiota output *always* has `F == T`
  (genuine polymorphism uses the base for **both** the generic argument and the factory), so the rewrite
  repairs the broken variant and is a **no-op** on the good one. **Consequence of git-ignoring the
  output:** a fresh-checkout or spec-change regeneration can still land on the dropped-type variant (a);
  that remains **not silent** — it fails the compile (red build) rather than shipping a wrong binary. If a
  build fails with a **missing-type** error under `Generated/`, just **build again** (regeneration
  re-rolls); the factory/return-type (CS0407) class no longer recurs, since the repair neutralizes it.
  Re-evaluate committing the output — or dropping these workarounds — when bumping the pinned Kiota
  version, in case determinism is restored upstream.
- **Kiota collapses the OAS cursor-pagination `page` parameters to a plain scalar.** Both
  `components/parameters/CursorPaginationPage` (a `deepObject` `page` with `size`/`after`/`before`) and
  `DualPaginationPage` (a discriminator-less `oneOf [integer, cursor object]`) generate as a single
  `int? Page` query property, so the generated builders cannot emit the OAS-modeled
  `page[size]`/`page[after]` pair. This — not a spec omission — is why `WithCursorPagination` rides those
  values on. **Criterion (keeps this list self-maintaining):** every list operation whose OAS `get`
  references `CursorPaginationPage` or `DualPaginationPage` needs the escape hatch; after a regeneration,
  re-derive the set with `grep -n 'CursorPaginationPage\|DualPaginationPage' support-oas.yaml` and confirm
  each match sits under a **collection** `get` (item-path and non-list refs don't count). The current
  inventory is 11 Support-API list endpoints:
  `GET /api/v2/tickets` (`tickets_list`), `GET /api/v2/users` (`users_list`),
  `GET /api/v2/users/{user_id}/identities` (`users_identities_list`),
  `GET /api/v2/organizations` (`organizations_list`), `GET /api/v2/views` (`views_list`),
  `GET /api/v2/ticket_forms` (`forms_list`), `GET /api/v2/ticket_fields` (`ticket_fields_list`),
  `GET /api/v2/brands` (`brands_list`), `GET /api/v2/suspended_tickets` (`suspended_tickets_list`),
  `GET /api/v2/job_statuses` (`job_statuses_list`) and `GET /api/v2/tags` (`tags_list`). The wire
  parameters are OAS-covered, so these are deliberately **not** spec-anomaly rows below; the escape hatch
  stays mechanically required after any regeneration. (The Help Center `articles_list` also rides
  `WithCursorPagination`, but there the cursor params are **not** OAS-modeled at all — it is a spec-anomaly
  row below, not part of this criterion.)
- **Kiota collapses `{id}.json` leaf paths into extension-less `{id}` item nodes**, silently defeating
  the P5 patch for the Help Center article/section/category **item** builders (tenant and locale
  variants, plus the unused `Comment_ItemRequestBuilder`) — the normalized spec carries
  `/articles/{article_id}.json` but the generated item templates end in `{article_%2Did}`. P5 exists
  because extension-less Help Center paths returned HTTP **415** in live testing even with JSON headers,
  so the MCP host's `articles_get` / `articles_sections_get` / `articles_categories_get` re-append the
  suffix via the `WithJsonSuffix` escape hatch (collection paths like `articles.json` keep their suffix
  and are unaffected).

## Known spec-fidelity hazards

The spec is the *scaffold*, not the truth — it has been proven wrong against live tenants. Anything
listed here must stay covered by curated code and wire-fidelity tests rather than trust in the spec:

- `GET /api/v2/tickets/{id}/metric_events.json` is documented but **does not exist** on real tenants
  (returns `200 {}`); metric events must be read via `incremental/ticket_metric_events.json`.
- P5 (`.json` suffix) is live-verified for the hand-written client; re-verify against a live tenant
  when the generated Help Center client first ships.
- Constraints (min/max/length) are almost entirely absent from the spec (~13 in 47k lines); the
  authoritative limits live in the HTML docs at developer.zendesk.com.
- The spec declares an `Internal` tag ("Internal APIs for Zendesk services only") but no operation
  currently uses it. If operations start carrying it, strip them here before generation.
- No OpenAPI spec exists for Chat or Sell (Support, Help Center, and Talk are published).

## Spec-anomaly ledger (live-API behavior the spec omits)

Query parameters, request bodies, and endpoints the **live API supports but the published OAS does not
model**. The MCP host rides them via the `ZendeskKiotaRequests` escape hatches (`WithQuery` /
`WithCursorPagination` / manual `RequestInformation`), manually attached bodies (`SetStreamContent` /
`SetContentFromParsable`), and Kiota `AdditionalData` passthrough (request-body properties the spec
omits — or marks `readOnly` against documented write behavior), so a Kiota regeneration or
spec-snapshot refresh cannot silently regress them — the anomalies live in curated code, not in the
generated builders. Unlike query-parameter rows, missing-requestBody rows regress *compile-loudly* on a
spec refresh (the generated builder signatures change); their rows are still inventory, tracking the
move-to-generated/retire-the-row workflow. If a future spec revision starts modeling one of these, move
the tool onto the generated parameter/body and retire its ledger row.

| Endpoint | Anomaly | Ridden by |
| --- | --- | --- |
| `GET /api/v2/incremental/tickets/cursor` | `per_page` is live-API-supported (Zendesk's incremental export docs, max 1000) but absent from the OAS, which models only `cursor`/`start_time`/`support_type_scope` | `tickets_export_incremental` via `WithQuery` (explicit default 100) |
| `GET /api/v2/incremental/tickets/cursor` | `include` sideloads are live-supported but absent from the OAS | `tickets_export_incremental` via `WithInclude` |
| `GET /api/v2/tickets/{id}/audits` | `per_page` is live-supported but absent from the OAS (which does model `page`/`sort_order`) | `tickets_audits_list` via `WithQuery` |
| `GET /api/v2/tickets/{id}/incidents` | The OAS models no paging parameters at all; the live API supports `page`/`per_page` | `tickets_incidents_list` via `WithQuery` |
| `GET /api/v2/search` | `page`/`per_page` are live-supported but absent from the OAS | `tickets_search` via `WithQuery` |
| `GET /api/v2/search` | Sideloads require the doc-only nested value syntax `include={type}({sideload})` (e.g. `include=tickets(users,organizations)`) per <https://developer.zendesk.com/documentation/ticketing/using-the-zendesk-api/side_loading/> — the OAS `SearchInclude` parameter models only a flat comma-separated list (example `users,organizations`) | `tickets_search` via the generated `Include` query parameter carrying the nested value (no escape hatch — the parameter is OAS-modeled, only the value shape is doc-only) |
| `GET /api/v2/tickets/{id}/side_conversations` | The endpoint is absent from the published spec entirely | `tickets_side_conversations_list` via a manual `RequestInformation` |
| `GET /api/v2/tickets/{id}/comments` | The cursor-pagination `sort` parameter is prose-only in the OAS (described in the operation text, never modeled); ordering is deliberately done via the OAS-modeled offset `sort_order` instead | `tickets_comments_list` `order` parameter (generated `sort_order`) |
| `GET /api/v2/incremental/tickets/cursor`, `GET /api/v2/incremental/ticket_metric_events` | `start_time` is typed `int` in the OAS (breaks Unix epochs past 2038) | `tickets_export_incremental` / `tickets_metric_events_export` add a `long` via raw `QueryParameters` |
| `GET /api/v2/users/{user_id}/tickets/assigned` | The OAS models no query parameters at all (only the `UserId` path param); the live API supports offset paging (`page`/`per_page`) and `include` sideloads (`users`, `groups`, `organizations`) per the [List User Assigned Tickets doc](https://developer.zendesk.com/api-reference/ticketing/tickets/tickets/#list-user-assigned-tickets) — the OAS's own sibling `/users/{user_id}/tickets/requested` models them via shared components | `users_tickets_assigned_list` via `WithQuery`/`WithInclude` |
| `GET /api/v2/users/{user_id}/tickets/ccd` | The OAS models no query parameters at all (only the `UserId` path param); the live API supports offset paging (`page`/`per_page`, max 100/page) and `include` sideloads per the [List User CCD Tickets doc](https://developer.zendesk.com/api-reference/ticketing/tickets/tickets/#list-user-ccd-tickets) | `users_tickets_ccd_list` via `WithQuery`/`WithInclude` |
| `GET /api/v2/organizations/{organization_id}/tickets` | The OAS models no query parameters at all (prose documents cursor+offset pagination); the live API supports `page`/`per_page` (max 100/page per the [pagination guide](https://developer.zendesk.com/api-reference/introduction/pagination/)) | `organizations_tickets_list` via `WithQuery` |
| `GET /api/v2/organizations/{organization_id}/tickets` | `include` sideloads (`users`, `groups`, `organizations`) are live-supported ([side-loading doc](https://developer.zendesk.com/documentation/ticketing/using-the-zendesk-api/side_loading/) plus the [List Tickets reference](https://developer.zendesk.com/api-reference/ticketing/tickets/tickets/#list-tickets), which lists this route) but absent from the OAS, which models no query parameters on this operation | `organizations_tickets_list` via `WithInclude` |
| `GET /api/v2/organizations/{organization_id}/users` | `include` sideloads (`organizations`, `groups`, `identities`) are live-supported ([side-loading doc](https://developer.zendesk.com/documentation/ticketing/using-the-zendesk-api/side_loading/)) but absent from the OAS, which models only role/permission_set/external_id/sort/paging parameters | `organizations_users_list` via `WithInclude` |
| `GET /api/v2/organizations/{organization_id}/organization_memberships` | The OAS models no paging parameters at all (pagination is prose-only in the operation description); the live API supports `page`/`per_page` (offset pagination, max 100/page) | `organizations_memberships_list` via `WithQuery` |
| `GET /api/v2/organizations/autocomplete` | Offset paging (`page`/`per_page`) is doc-supported (the operation prose says "Offset pagination only", deferring to the global offset-pagination parameters) but the OAS models no paging parameters | `organizations_autocomplete` via `WithQuery` |
| `GET /api/v2/organizations/search` | `external_id` is typed `integer` in the OAS parameter (`OrganizationExternalId`, example 1234) but live external ids are opaque case-insensitive strings — the Organization record schema in the same OAS and the docs' JSON format table both type it `string` | `organizations_get_by_name_or_external_id` adds the string via raw `QueryParameters` |
| `GET /api/v2/groups/{group_id}/memberships` | Offset pagination (`page`/`per_page`) is prose-only (the operation models no parameters) and `include` sideloads (valid values `users`, `groups` per the [group memberships doc](https://developer.zendesk.com/api-reference/ticketing/groups/group_memberships/)) are absent on this path variant — the spec's own `GroupMembershipsInclude` component is wired only to `/api/v2/group_memberships` and `/api/v2/users/{user_id}/group_memberships` | `groups_memberships_list` via `WithQuery`/`WithInclude` |
| `GET /api/v2/groups/assignable` | The OAS models no query parameters at all; offset pagination (`page`/`per_page`, max 100/page) is prose-only in the operation description (and in the [Groups API doc](https://developer.zendesk.com/api-reference/ticketing/groups/groups/)) | `groups_assignable_list` via `WithQuery` |
| `GET /api/v2/groups/{group_id}/users` | Offset pagination (`page`/`per_page`) is prose-only in the OAS (description text), never modeled — the operation models only the role/role[]/permission_set/external_id filters ([List Users By Group](https://developer.zendesk.com/api-reference/ticketing/users/users/#list-users-by-group)) | `groups_users_list` via `WithQuery` |
| `GET /api/v2/views/{view_id}/tickets` | `page`/`per_page` (offset pagination) are documented — the operation's own Pagination prose plus the [pagination guide](https://developer.zendesk.com/api-reference/introduction/pagination/) (max 100/page) — but the OAS models only `sort_by`/`sort_order`, never the paging parameters | `views_tickets_list` via `WithQuery` |
| `GET /api/v2/macros/active` | Offset `page`/`per_page` are absent from the OAS (which models only include/access/category/group_id/sort_by/sort_order) but officially supported per the [pagination introduction](https://developer.zendesk.com/api-reference/introduction/pagination/)'s default rule ("If no pagination method is specified, the endpoint only supports offset pagination") | `macros_list_active` via `WithQuery` |
| `GET /api/v2/ticket_fields/{ticket_field_id}/options` | The OAS models no query parameters at all; offset `page`/`per_page` is prose-only in the operation description (doc-verified via the [pagination guide](https://developer.zendesk.com/api-reference/introduction/pagination/); caveat: [zendesk_api_client_php#417](https://github.com/zendesk/zendesk_api_client_php/issues/417) reports the endpoint sometimes ignores `page`/`per_page` and returns the full list) | `ticket_fields_options_list` via `WithQuery` |
| `GET /api/v2/help_center[/{locale}][/sections/{section_id}]/articles` | Cursor pagination (`page[size]`/`page[after]`) is live-supported ([Articles doc](https://developer.zendesk.com/api-reference/help_center/help-center-api/articles/), List Articles Pagination section) but the OAS models only `sort_by`/`sort_order`/`start_time`/`label_names` on all four list-articles operations | `articles_list` via `WithCursorPagination` |
| `GET /api/v2/help_center[/{locale}][/sections/{section_id}]/articles` | `include` sideloads (`users`, `sections`, `categories`, `translations` — translations embedded within each article) are doc-supported ([List Articles Sideloads](https://developer.zendesk.com/api-reference/help_center/help-center-api/articles/)) but absent from the OAS list operations (the sideload table appears only under Show Article) | `articles_list` via `WithInclude` |
| `GET /api/v2/help_center[/{locale}]/categories`, `GET /api/v2/help_center[/{locale}][/categories/{category_id}]/sections`, `GET /api/v2/help_center/articles/search` | Offset pagination (`page`/`per_page`, max 100/page per the [pagination guide](https://developer.zendesk.com/api-reference/introduction/pagination/)) is prose-only ("Offset pagination" under each operation's Pagination section) or absent in the OAS and never modeled as parameters | `articles_categories_list` / `articles_sections_list` / `articles_search` via `WithQuery` |
| `POST/PUT/DELETE /api/v2/tickets/{ticket_id}/tags` | The documented JSON request body (`{tags, updated_stamp, safe_update:"true"}` — the PUT describes it only in prose, the DELETE models only a `tags` *query* parameter, the POST models nothing) is absent from the OAS `requestBody` ([Tags doc](https://developer.zendesk.com/api-reference/ticketing/ticket-management/tags/)) | `tickets_tags_set` / `tickets_tags_add` / `tickets_tags_remove` via `SendTagsAsync` (manual `SetStreamContent` on the builder-produced `RequestInformation`) |
| `POST /api/v2/imports/tickets`, `POST /api/v2/imports/tickets/create_many` | Import comment `created_at` is doc-supported request-side ([Ticket Import doc](https://developer.zendesk.com/api-reference/ticketing/tickets/ticket_import/): "You can also set the comment's created_at time stamp") but marked `readOnly` on `TicketCommentObject`, so the generated serializer silently drops it (the comment objects themselves ARE modeled) | `tickets_import` / `tickets_import_many` via `TicketImportInput.AdditionalData["comments"]` |
| `POST /api/v2/organizations/create_many`, `POST /api/v2/organizations/create_or_update`, `PUT /api/v2/organizations/{organization_id}`, `PUT /api/v2/organizations/update_many` | The OAS declares no `requestBody` on these operations; the live API takes the documented `{"organization": {...}}` / `{"organizations": [...]}` envelopes ([Organizations doc](https://developer.zendesk.com/api-reference/ticketing/organizations/organizations/)) | `organizations_create_many` / `organizations_create_or_update` / `organizations_update` / `organizations_update_many(_batch)` via `SetContentFromParsable` |
| `POST /api/v2/organization_memberships`, `POST /api/v2/organization_memberships/create_many` | The OAS models no `requestBody` (`create_many` prose says "Accepts an array of up to 100 organization membership objects" but never names the envelope) and marks `user_id`/`organization_id` `readOnly` on `OrganizationMembershipObject` despite listing them as required, so the generated serializer emits only `default`; the live API takes `{"organization_membership": {...}}` / `{"organization_memberships": [...]}` with writable ids ([Create Membership](https://developer.zendesk.com/api-reference/ticketing/organizations/organization_memberships/#create-membership), [Create Many Memberships](https://developer.zendesk.com/api-reference/ticketing/organizations/organization_memberships/#create-many-memberships)) | `organizations_memberships_create` / `organizations_memberships_create_many` via `SetContentFromParsable` + `AdditionalData` |
| `DELETE /api/v2/organization_memberships/destroy_many` | `ids` is typed as an exploded int64 array in the OAS (generated builder: `long?[]` with `{?ids*}` → `ids=1&ids=2`), but the live API/docs expect a comma-separated string ([Bulk Delete Memberships](https://developer.zendesk.com/api-reference/ticketing/organizations/organization_memberships/#bulk-delete-memberships), curl example `?ids=1,2,3`) — the only bulk `destroy_many` endpoint not typed `string` (cf. `group_memberships/destroy_many`, shared `OrganizationIds`/`TicketIds` params) | `organizations_memberships_delete_many` via raw `QueryParameters` CSV join |
| `POST /api/v2/users/{user_id}/identities`, `PUT /api/v2/users/{user_id}/identities/{identity_id}` | The OAS declares no `requestBody`; the live API takes the documented `{"identity": {...}}` envelope ([User Identities doc](https://developer.zendesk.com/api-reference/ticketing/users/user_identities/)) | `users_identities_create` / `users_identities_update` via `SetStreamContent` |
| `POST /api/v2/macros`, `PUT /api/v2/macros/{macro_id}` | `position` is documented as writable (docs JSON table: integer, read-only false; explicitly listed under Create Macro body properties at <https://developer.zendesk.com/api-reference/ticketing/business-rules/macros/>) but absent from the OAS `MacroInput` request schema (which models only actions/active/description/restriction/title; the OAS itself models `position` as writable on `MacroCommonObject` and `MacroUpdateManyInput`). Doc-verified, not yet live-verified | `macros_create` / `macros_update` via `MacroInput.AdditionalData["position"]` (serializes as a top-level macro field) |
| `POST /api/v2/macros`, `PUT /api/v2/macros/{macro_id}` | `ActionObject.value` is string-typed in the OAS, but the live API accepts (and returns) array values for multi-value macro actions, e.g. `comment_value` as `[channel, text]` — per the [Actions reference](https://developer.zendesk.com/documentation/ticketing/reference-guides/actions-reference/) the OAS itself links; note `TriggerActionObject` models the same multi-value shape as `oneOf`, so this is a per-schema spec omission | `macros_create` / `macros_update` via `ActionObject.AdditionalData["value"]` (request side); raw wire-JSON parsing of the echoed macro (response side) |
| `POST /api/v2/ticket_fields`, `PUT /api/v2/ticket_fields/{ticket_field_id}`, `POST /api/v2/ticket_fields/{ticket_field_id}/options` | The OAS declares no `requestBody` (only `responses`); the documented `{"ticket_field": {...}}` / `{"custom_field_option": {...}}` envelopes ([Ticket Fields doc](https://developer.zendesk.com/api-reference/ticketing/tickets/ticket_fields/)) are attached in curated code using the OAS component schemas (`TicketFieldResponse`/`CustomFieldOptionResponse`) | `ticket_fields_create` / `ticket_fields_update` / `ticket_fields_options_create_or_update` via `SetContentFromParsable` |
| `POST /api/v2/ticket_forms`, `PUT /api/v2/ticket_forms/{ticket_form_id}` | The OAS defines no `requestBody` on create/update; the live API/docs require the `{"ticket_form": {...}}` envelope (name, display_name, position, active, default, end_user_visible, in_all_brands, ticket_field_ids — all non-readOnly in the OAS `TicketFormObject`; [Ticket Forms doc](https://developer.zendesk.com/api-reference/ticketing/tickets/ticket_forms/#create-ticket-form)) | `forms_create` / `forms_update` via `WithTicketFormBody` (curated `ZendeskTicketFormWrite` model + `SetStreamContent`) |
| `POST /api/v2/uploads` | The binary file request body (raw bytes, typed by the `Content-Type` header) is doc-described ([Adding ticket attachments](https://developer.zendesk.com/documentation/ticketing/managing-tickets/adding-ticket-attachments-with-the-api/), `--data-binary` + `Content-Type` curl example) but absent from the OAS — the operation models no `requestBody` | `uploads_create` via `SetStreamContent` on the builder-produced `RequestInformation` |
| `POST /api/v2/uploads` | The `token` (multi-file chaining) query parameter is live-supported and shown in the [official doc's curl example](https://developer.zendesk.com/api-reference/ticketing/tickets/ticket-attachments/#upload-files) (and mentioned in the OAS description prose) but never modeled as a parameter — the OAS models only `filename` | `uploads_create` via RFC 6570 continuation (`UrlTemplate += "{&token}"`) + raw `QueryParameters` |
