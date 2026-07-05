using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.Support;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for Zendesk users. Namespaced <c>users_*</c> to mirror the Zendesk API structure.
/// </summary>
/// <remarks>
///     Requests are built through the generated request builders, but responses are read as the raw wire
///     JSON (<see cref="ZendeskKiotaRequests.SendForJsonAsync" />) rather than round-tripped through the
///     generated models: the published spec marks server-assigned fields (user <c>id</c>/<c>created_at</c>/
///     <c>updated_at</c>/<c>active</c>, the list envelopes' <c>count</c>/<c>next_page</c>/<c>meta</c>,
///     the whole related-information object, ...) as read-only, so Kiota's serializer would silently drop them
///     from the tool result. List/search responses are then projected through <see cref="ZendeskLean" /> into
///     the uniform lean envelope — summary rows by default, complete records via <c>detail:'full'</c> or
///     <c>users_get</c>.
/// </remarks>
[McpServerToolType]
public sealed class ZendeskUserTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IOptionsMonitor<McpOptions> mcpOptions)
{
    /// <summary>Zendesk's documented maximum for <c>show_many</c>; larger lists are rejected with 400.</summary>
    private const int MaxIdsPerShowManyRequest = 100;

    /// <summary>The uniform <c>detail</c> parameter description shared by the list/search tools.</summary>
    private const string DetailDescription =
        "Row detail: 'summary' (default; lean triage rows) | 'full' (complete records minus null fields and API links).";

    /// <summary>Returns the Zendesk user associated with the configured credentials.</summary>
    [McpServerTool(Name = "users_me_get", ReadOnly = true, OpenWorld = false)]
    [Description(
        "User for the configured API credentials (authenticated account). Full-detail record (null fields and " +
        "API self-links omitted; absent field = null/empty). Verifies connectivity/identity. Read-only.")]
    public Task<JsonElement> Whoami(CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var request = zendesk.Api.V2.Users.Me.ToGetRequestInformation();
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("user", out var user)
                ? ZendeskLean.EnsureWithinBudget(ZendeskLean.ToFullView(user), "users_me_get",
                    MaxResponseChars("users_me_get"),
                    "User record too large — fetch parts via users_identities_list / users_organizations_list.")
                : throw new McpException("Zendesk returned no user for the configured credentials.");
        });

    /// <summary>Returns a Zendesk user by id.</summary>
    [McpServerTool(Name = "users_get", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Single user by numeric id. Full-detail record (null fields and API self-links omitted; absent field = " +
        "null/empty), incl. custom user_fields and photo. Detail sink for the lean rows from users_* list/search " +
        "tools. Read-only.")]
    public Task<JsonElement> Read(
        [Description("Numeric Zendesk user id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var request = zendesk.Api.V2.Users[id].ToGetRequestInformation();
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("user", out var user)
                ? ZendeskLean.EnsureWithinBudget(ZendeskLean.ToFullView(user), "users_get",
                    MaxResponseChars("users_get"),
                    "User record too large — fetch parts via users_identities_list / users_organizations_list.")
                : throw new McpException($"Zendesk user '{id}' was not found.");
        });

    /// <summary>Searches Zendesk users.</summary>
    [McpServerTool(Name = "users_search", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Searches users. Rows are lean user summaries (id, name, email, role, active/suspended flags, " +
        "organization_id, phone, last_login_at, external_id); detail:'full' for complete records, or users_get " +
        "for one. organization_id resolves to a name via organizations_get_many. Default perPage 25 (max 100); " +
        "total match count in 'count'. Read-only.")]
    public Task<JsonElement> Search(
        [Description(
            "Zendesk search syntax. Bare term = partial/full match on name, email, notes, phone (e.g. \"jdoe\"). " +
            "field:value terms implicit-AND; repeat a field to OR its values; '-' prefix excludes; operators : < " +
            "> <= >=. Filters: role:end-user|agent|admin (or custom role name), email:jane@example.com, " +
            "group:\"Level 2\", organization:mondocam (org name or id), tags:premium, name/phone/notes/external_id " +
            "(value/free text), is_verified:true|false, is_suspended:true|false, created<2011-05-01. " +
            "created/updated take YYYY-MM-DD, ISO8601, or relative (created>7days). type:user forces a user " +
            "search. Example: \"role:agent tags:vip created>30days\".")]
        string query,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100). Offset pagination only; <=10,000 total records per query " +
            "(paging past returns an error) — narrow the query, don't page deeper. Total match count in 'count'.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var request = zendesk.Api.V2.Users.Search.ToGetRequestInformation(cfg =>
            {
                cfg.QueryParameters.Query = query;
                cfg.QueryParameters.Page = page;
                cfg.QueryParameters.PerPage = perPage;
            });
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "users", page, parsedDetail,
                MaxResponseChars("users_search"));
        });

    /// <summary>Returns many users by id in one call (batch resolution).</summary>
    [McpServerTool(Name = "users_get_many", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Many users by id in one call — resolve requester/assignee/author/CC ids on tickets, comments, audits to " +
        "names/emails without one call per id. Hard cap 100 ids/call (show_many limit); for more, split into " +
        "batches of 100 and call per batch. Rows are lean summaries; detail:'full' for complete records, or " +
        "users_get for one. Read-only.")]
    public Task<JsonElement> ReadMany(
        [Description("Numeric user ids to resolve (<=100 per call).")]
        long[] ids,
        [Description(
            "Sideloads returned as sibling arrays (summary-projected): any of \"organizations\", \"groups\", " +
            "\"identities\".")]
        string[]? include = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // show_many rejects more than 100 ids with 400 Bad Request — surface the API contract as an
            // actionable batching instruction instead of silently fanning out server-side.
            if (ids.Length > MaxIdsPerShowManyRequest)
                throw new McpException(
                    $"users_get_many accepts at most {MaxIdsPerShowManyRequest} ids per call (Zendesk's " +
                    $"show_many limit) but was passed {ids.Length}. Split the ids into batches of " +
                    $"{MaxIdsPerShowManyRequest} and call once per batch.");
            if (ids.Length == 0)
                return ZendeskLean.BuildOffsetListEnvelope(EmptyUsersResponse(), "users", null,
                    parsedDetail, MaxResponseChars("users_get_many"));

            var request = zendesk.Api.V2.Users.Show_many.ToGetRequestInformation(cfg =>
            {
                cfg.QueryParameters.Ids = string.Join(',', ids);
                cfg.QueryParameters.Include = JoinInclude(include);
            });
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "users", null, parsedDetail,
                MaxResponseChars("users_get_many"));
        });

    /// <summary>Returns the tickets a user has requested (their ticket history).</summary>
    [McpServerTool(Name = "users_tickets_requested_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Tickets requested by a user (their support history) as lean ticket summary rows (id, subject, 150-char " +
        "description excerpt, status, priority, type, dates, people ids, tags, via.channel); detail:'full' for " +
        "complete records, or tickets_get for one. Context: prior issues, repeat complaints, what was tried, " +
        "reopen/duplicate. Default perPage 25 (max 100); for just the how-many number read counts in " +
        "users_related_get. To filter by status or date, use tickets_search with requester:<id> (e.g. " +
        "requester:<id> status<solved). Read-only.")]
    public Task<JsonElement> RequestedTickets(
        [Description("Numeric user id (the requester).")]
        long userId,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100). Total in 'count'; response's has_more/next_page drive paging.")]
        int? perPage = 25,
        [Description(
            "Sideloads resolved inline as sibling arrays (summary-projected): \"users\", \"groups\", " +
            "\"organizations\", \"brands\", \"custom_statuses\" (human-readable custom-status labels), " +
            "\"ticket_forms\", \"metric_sets\", \"dates\", \"last_audits\", \"sharing_agreements\", " +
            "\"incident_counts\". (slas, metric_events are single-ticket only — use tickets_get.)")]
        string[]? include = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var request = zendesk.Api.V2.Users[userId].Tickets.Requested.ToGetRequestInformation(cfg =>
            {
                cfg.QueryParameters.Page = page;
                cfg.QueryParameters.PerPage = perPage;
                cfg.QueryParameters.Include = JoinInclude(include);
            });
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "tickets", page, parsedDetail,
                MaxResponseChars("users_tickets_requested_list"));
        });

    /// <summary>Lists Zendesk users, optionally filtered by role.</summary>
    [McpServerTool(Name = "users_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists users as lean summary rows (id, name, email, role, active/suspended flags, organization_id, phone, " +
        "last_login_at, external_id), optionally filtered by role (\"end-user\", \"agent\", \"admin\"); " +
        "detail:'full' for complete records, or users_get for one. Prefer users_search/users_autocomplete for a " +
        "specific person. Cursor pagination: default pageSize 25 (max 100); response's has_more/after_cursor drive " +
        "continuation. Sideloads resolve related records inline (summary-projected). Read-only.")]
    public Task<JsonElement> List(
        [Description(
            "Optional role filter: \"end-user\", \"agent\", \"admin\", or a custom role name.")]
        string? role = null,
        [Description("Results per page (default 25; capped at 100).")]
        int? pageSize = 25,
        [Description(
            "Continuation cursor for the NEXT page — OMIT for the first page. When present, the opaque token copied " +
            "verbatim from the previous response's after_cursor; not a page number, don't guess or pass empty " +
            "(invalid cursor rejected with 400).")]
        string? afterCursor = null,
        [Description(
            "Sideloads resolved inline as sibling arrays: any of \"organizations\", \"groups\", \"identities\".")]
        string[]? include = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // The generated builder only exposes offset paging; the live API also supports cursor pagination.
            var request = zendesk.Api.V2.Users.ToGetRequestInformation(cfg =>
                {
                    cfg.QueryParameters.Role = role;
                    cfg.QueryParameters.Include = JoinInclude(include);
                })
                .WithCursorPagination(pageSize, afterCursor);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildCursorListEnvelope(json, "users", parsedDetail,
                MaxResponseChars("users_list"));
        });

    /// <summary>Returns the (cached, approximate) user count, optionally filtered by role.</summary>
    [McpServerTool(Name = "users_count", ReadOnly = true, OpenWorld = false)]
    [Description(
        "User count, optionally filtered by role (\"end-user\", \"agent\", \"admin\", or a custom role name). " +
        "Approximate: if >100,000 it is recomputed ~every 24h, value capped at 100,000 until refresh completes, " +
        "and refreshed_at may be null while recomputing in the background. For the count of a filtered subset use " +
        "users_search and read 'count'. Read-only.")]
    public Task<JsonElement> Count(
        [Description(
            "Optional role filter: \"end-user\", \"agent\", \"admin\", or a custom role name.")]
        string? role = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() => requestAdapter.SendForJsonAsync(
            zendesk.Api.V2.Users.Count.ToGetRequestInformation(cfg => cfg.QueryParameters.Role = role),
            cancellationToken));

    /// <summary>Suggests users whose name starts with a prefix.</summary>
    [McpServerTool(Name = "users_autocomplete", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Suggests users whose name starts with a prefix — cheap type-ahead for a partial name. Rows are lean user " +
        "summaries; detail:'full' for complete records, or users_get for one. organization_id resolves to a name " +
        "via organizations_get_many. Single top-N suggestion list (default 10, max 100 via perPage) — not " +
        "pageable; to widen, raise perPage or refine the prefix. Read-only.")]
    public Task<JsonElement> Autocomplete(
        [Description(
            "Name value to match. Matches users whose name STARTS WITH this (prefix, not substring). Only returns " +
            "users with no foreign identities.")]
        string name,
        [Description("Max suggestions (default 10, max 100).")]
        int? perPage = 10,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            // Both parameters are OAS-modeled; per_page only limits the number of suggestions returned — the
            // endpoint has no offset paging (no 'page' parameter, and its response carries no count/next_page).
            var request = zendesk.Api.V2.Users.Autocomplete.ToGetRequestInformation(cfg =>
            {
                cfg.QueryParameters.Name = name;
                cfg.QueryParameters.PerPage = perPage;
            });
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "users", null, parsedDetail,
                MaxResponseChars("users_autocomplete"));
        });

    /// <summary>Returns a user's related ticket/subscription counts.</summary>
    [McpServerTool(Name = "users_related_get", ReadOnly = true, OpenWorld = false)]
    [Description(
        "User's related information — counts of requested/assigned tickets and subscriptions. Quick gauge of how " +
        "active a user is before pulling full ticket lists (users_tickets_requested_list / " +
        "users_tickets_assigned_list). Read-only.")]
    public Task<JsonElement> Related(
        [Description("Numeric Zendesk user id.")]
        long userId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => requestAdapter.SendForJsonAsync(
            zendesk.Api.V2.Users[userId].Related.ToGetRequestInformation(), cancellationToken));

    /// <summary>Lists a user's identities (e-mails, phone numbers, social handles).</summary>
    [McpServerTool(Name = "users_identities_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists a user's identities — e-mail addresses, phone numbers, social handles — as lean identity rows (id, " +
        "user_id, type, value, primary/verified flags); detail:'full' for complete records. All contact points " +
        "behind a user record. Cursor pagination: default pageSize 25 (max 100); response's has_more/after_cursor " +
        "drive continuation. Read-only.")]
    public Task<JsonElement> Identities(
        [Description("Numeric Zendesk user id.")]
        long userId,
        [Description("Results per page (default 25; capped at 100).")]
        int? pageSize = 25,
        [Description(
            "Continuation cursor for the NEXT page — OMIT for the first page. When present, the opaque token copied " +
            "verbatim from the previous response's after_cursor; not a page number, don't guess or pass empty " +
            "(invalid cursor rejected with 400).")]
        string? afterCursor = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // The generated builder only models offset paging; the live API also supports cursor pagination.
            var request = zendesk.Api.V2.Users[userId].Identities.ToGetRequestInformation()
                .WithCursorPagination(pageSize, afterCursor);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildCursorListEnvelope(json, "identities", parsedDetail,
                MaxResponseChars("users_identities_list"));
        });

    /// <summary>Lists the groups an agent belongs to.</summary>
    [McpServerTool(Name = "users_groups_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the groups an agent belongs to — teams their tickets can route through — as lean group rows (id, " +
        "name, description, default, deleted, is_public); detail:'full' for complete records, or groups_get for " +
        "one. Complements groups_memberships_list (group → agents). Default perPage 25 (max 100); " +
        "count/has_more/next_page drive paging. Read-only.")]
    public Task<JsonElement> Groups(
        [Description("Numeric user id (an agent).")]
        long userId,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100). Total in 'count'; response's has_more/next_page drive paging.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var request = zendesk.Api.V2.Users[userId].Groups.ToGetRequestInformation(cfg =>
            {
                cfg.QueryParameters.Page = page;
                cfg.QueryParameters.PerPage = perPage;
            });
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "groups", page, parsedDetail,
                MaxResponseChars("users_groups_list"));
        });

    /// <summary>Lists the organizations a user belongs to.</summary>
    [McpServerTool(Name = "users_organizations_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the organizations a user belongs to — companies/accounts their tickets are attributed to — as lean " +
        "organization rows (id, name, domain_names, external_id, sharing flags, tags, dates); detail:'full' for " +
        "complete records, or organizations_get for one. Default perPage 25 (max 100); count/has_more/next_page " +
        "drive paging. Read-only.")]
    public Task<JsonElement> Organizations(
        [Description("Numeric Zendesk user id.")]
        long userId,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100). Total in 'count'; response's has_more/next_page drive paging.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var request = zendesk.Api.V2.Users[userId].Organizations.ToGetRequestInformation(cfg =>
            {
                cfg.QueryParameters.Page = page;
                cfg.QueryParameters.PerPage = perPage;
            });
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "organizations", page, parsedDetail,
                MaxResponseChars("users_organizations_list"));
        });

    /// <summary>Returns the tickets assigned to an agent.</summary>
    [McpServerTool(Name = "users_tickets_assigned_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Tickets assigned to an agent — current and past workload — as lean ticket summary rows; detail:'full' " +
        "for complete records, or tickets_get for one. Default perPage 25 (max 100); count/has_more/next_page " +
        "drive paging. For just the workload number read counts in users_related_get. To filter by status or " +
        "date, use tickets_search with assignee:<id> (e.g. assignee:<id> status<solved). Read-only.")]
    public Task<JsonElement> AssignedTickets(
        [Description("Numeric user id (the assignee agent).")]
        long userId,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100). Total in 'count'; response's has_more/next_page drive paging.")]
        int? perPage = 25,
        [Description(
            "Sideloads resolved inline as sibling arrays: any of \"users\", \"groups\", \"organizations\".")]
        string[]? include = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // The generated builder exposes no query parameters; the live endpoint accepts offset paging and
            // sideloads per the List User Assigned Tickets doc
            // (https://developer.zendesk.com/api-reference/ticketing/tickets/tickets/#list-user-assigned-tickets)
            // — ledger row in src/ES.FX.Zendesk/OpenApi/README.md.
            var request = zendesk.Api.V2.Users[userId].Tickets.Assigned.ToGetRequestInformation()
                .WithQuery("page", page)
                .WithQuery("per_page", perPage)
                .WithInclude(JoinInclude(include));
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "tickets", page, parsedDetail,
                MaxResponseChars("users_tickets_assigned_list"));
        });

    /// <summary>Returns the tickets a user is CC'd on.</summary>
    [McpServerTool(Name = "users_tickets_ccd_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Tickets a user is CC'd on — issues they follow without being requester or assignee — as lean ticket " +
        "summary rows; detail:'full' for complete records, or tickets_get for one. Default perPage 25 (max 100); " +
        "count/has_more/next_page drive paging. To filter by status or date, use tickets_search with cc:<id> " +
        "(e.g. cc:<id> status<solved). Read-only.")]
    public Task<JsonElement> CcdTickets(
        [Description("Numeric user id (the CC'd user).")]
        long userId,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100). Total in 'count'; response's has_more/next_page drive paging.")]
        int? perPage = 25,
        [Description(
            "Sideloads resolved inline as sibling arrays: any of \"users\", \"groups\", \"organizations\".")]
        string[]? include = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // The generated builder exposes no query parameters; the live endpoint accepts offset paging and
            // sideloads per the List User CCD Tickets doc
            // (https://developer.zendesk.com/api-reference/ticketing/tickets/tickets/#list-user-ccd-tickets)
            // — ledger row in src/ES.FX.Zendesk/OpenApi/README.md.
            var request = zendesk.Api.V2.Users[userId].Tickets.Ccd.ToGetRequestInformation()
                .WithQuery("page", page)
                .WithQuery("per_page", perPage)
                .WithInclude(JoinInclude(include));
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "tickets", page, parsedDetail,
                MaxResponseChars("users_tickets_ccd_list"));
        });

    /// <summary>Lists a user's tags.</summary>
    [McpServerTool(Name = "users_tags_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists a user's tags as plain strings. Requires user tagging enabled in Zendesk Support. Read-only.")]
    public Task<JsonElement> Tags(
        [Description("Numeric Zendesk user id.")]
        long userId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => requestAdapter.SendForJsonAsync(
            zendesk.Api.V2.Users[userId].Tags.ToGetRequestInformation(), cancellationToken));

    /// <summary>Resolves the response-size budget for a tool (see <see cref="McpToolsOptions.GetMaxResponseChars" />).</summary>
    private int MaxResponseChars(string toolName) => mcpOptions.CurrentValue.Tools.GetMaxResponseChars(toolName);

    /// <summary>
    ///     Builds the flat sideload value for list/show endpoints (e.g. <c>users,groups,organizations</c>), or
    ///     <c>null</c> (omitted) when nothing is requested.
    /// </summary>
    private static string? JoinInclude(string[]? include) =>
        include is null || include.Length == 0 ? null : string.Join(',', include);

    /// <summary>An empty wire-shaped users response, for the no-ids fast path of <c>users_get_many</c>.</summary>
    private static JsonElement EmptyUsersResponse() =>
        JsonSerializer.SerializeToElement(new JsonObject { ["users"] = new JsonArray() });
}