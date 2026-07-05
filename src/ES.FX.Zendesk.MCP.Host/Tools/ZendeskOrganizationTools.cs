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
///     MCP tools for Zendesk organizations. Namespaced <c>organizations_*</c> to mirror the Zendesk API.
/// </summary>
/// <remarks>
///     Requests are built through the generated request builders, but responses are read as the raw wire
///     JSON (<see cref="ZendeskKiotaRequests.SendForJsonAsync" />) rather than round-tripped through the
///     generated models: the published spec marks server-assigned fields (<c>created_at</c>, membership
///     <c>id</c>/<c>user_id</c>/<c>organization_id</c>, the whole <c>count</c> object, ...) as read-only, so
///     Kiota's serializer would silently drop them from the tool result. The escape hatches also supply the
///     query parameters the published spec omits (cursor pagination on <c>organizations</c>, offset paging and
///     sideloads on the sublists). List/lookup responses are then projected through <see cref="ZendeskLean" />
///     into the uniform lean envelope — summary rows by default, complete records via <c>detail:'full'</c> or
///     <c>organizations_get</c>.
/// </remarks>
[McpServerToolType]
public sealed class ZendeskOrganizationTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IOptionsMonitor<McpOptions> mcpOptions)
{
    /// <summary>Zendesk's documented maximum for <c>show_many</c>; larger lists are rejected with 400.</summary>
    private const int MaxIdsPerShowManyRequest = 100;

    /// <summary>The uniform <c>detail</c> parameter description shared by the list/lookup tools.</summary>
    private const string DetailDescription =
        "Row detail: 'summary' (default, lean triage rows) | 'full' (complete records minus null fields and " +
        "API links).";

    /// <summary>Returns a Zendesk organization by id.</summary>
    [McpServerTool(Name = "organizations_get", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Organization by numeric id. Full-detail record (null fields and API self-links omitted; absent " +
        "field = null/empty): account/company context — domains, custom org fields (e.g. plan/tier/region), " +
        "default routing group, tags, internal notes/details. ONLY place notes/details and custom org fields " +
        "are returned — summary rows never carry them.")]
    public Task<JsonElement> Read(
        [Description("Numeric Zendesk organization id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var request = zendesk.Api.V2.Organizations[id].ToGetRequestInformation();
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("organization", out var organization)
                ? ZendeskLean.EnsureWithinBudget(ZendeskLean.ToFullView(organization), "organizations_get",
                    MaxResponseChars("organizations_get"),
                    "Organization record too large — read related data via organizations_users_list / organizations_tickets_list.")
                : throw new McpException($"Zendesk organization '{id}' was not found.");
        });

    /// <summary>Returns the tickets belonging to an organization.</summary>
    [McpServerTool(Name = "organizations_tickets_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Account-wide ticket history for an organization (spot recurring/systemic issues, incidents affecting " +
        "one company). Rows: lean ticket summaries (id, subject, 150-char description excerpt, status, " +
        "priority, type, dates, people ids, tags, via.channel; default perPage 25). detail:'full' for complete " +
        "records, or tickets_get for one. For just the COUNT call organizations_tickets_count. Sideloads " +
        "resolve related records inline (summary-projected).")]
    public Task<JsonElement> Tickets(
        [Description("Numeric Zendesk organization id.")]
        long organizationId,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100; above 100 clamped). Total in 'count'; 'has_more'/'next_page' drive paging.")]
        int? perPage = 25,
        [Description(
            "Sideloads resolving ids inline in one call: any of \"users\", \"groups\", \"organizations\". Returned as sibling arrays.")]
        string[]? include = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // Spec gap: the generated builder exposes no paging or sideload parameters on this endpoint.
            var request = zendesk.Api.V2.Organizations[organizationId].Tickets.ToGetRequestInformation()
                .WithQuery("page", page)
                .WithQuery("per_page", perPage)
                .WithInclude(JoinInclude(include));
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "tickets", page, parsedDetail,
                MaxResponseChars("organizations_tickets_list"));
        });

    /// <summary>Returns the approximate ticket count of an organization.</summary>
    [McpServerTool(Name = "organizations_tickets_count", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Ticket count for an organization — cheaper than paging organizations_tickets_list to size the history. " +
        "When count exceeds 100,000 it refreshes only every 24h and 'value' is capped at 100,000 until the " +
        "background update completes ('refreshed_at' = cache time, may be null during that window). For an exact " +
        "filtered count use search_count (e.g. \"type:ticket organization:12345 status:open\").")]
    public Task<JsonElement> TicketsCount(
        [Description("Numeric Zendesk organization id.")]
        long organizationId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => requestAdapter.SendForJsonAsync(
            zendesk.Api.V2.Organizations[organizationId].Tickets.Count.ToGetRequestInformation(),
            cancellationToken));

    /// <summary>Lists Zendesk organizations.</summary>
    [McpServerTool(Name = "organizations_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Browse accounts/companies known to the instance as lean summary rows (id, name, domain_names, " +
        "external_id, shared_tickets/shared_comments, tags, dates). detail:'full' for complete records (custom " +
        "org fields, notes/details), or organizations_get for one. Cursor pagination: default pageSize 25 (max " +
        "100); response's has_more/after_cursor drive continuation. For a lookup by exact name or external id " +
        "use organizations_get_by_name_or_external_id. Restricted-scope custom agent roles (agents limited to " +
        "their own organization) get a 403.")]
    public Task<JsonElement> List(
        [Description("Results per page (default 25, max 100; larger capped at 100).")]
        int? pageSize = 25,
        [Description(
            "Continuation cursor for the NEXT page — OMIT for the first page. When present, must be the opaque " +
            "token copied verbatim from the previous response's after_cursor; not a page number, must not be " +
            "guessed or passed empty (invalid cursor rejected with 400).")]
        string? afterCursor = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // Spec gap: the generated builder only documents offset paging; the live API supports cursors.
            var request = zendesk.Api.V2.Organizations.ToGetRequestInformation()
                .WithCursorPagination(pageSize, afterCursor);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildCursorListEnvelope(json, "organizations", parsedDetail,
                MaxResponseChars("organizations_list"));
        });

    /// <summary>Returns the approximate organization count.</summary>
    [McpServerTool(Name = "organizations_count", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Count of Zendesk organizations. When count exceeds 100,000 it refreshes only every 24h and 'value' is " +
        "capped at 100,000 until the background update completes ('refreshed_at' = cache time, may be null " +
        "during that window). For an exact filtered count use search_count (e.g. \"type:organization " +
        "tags:vip\").")]
    public Task<JsonElement> Count(CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => requestAdapter.SendForJsonAsync(
            zendesk.Api.V2.Organizations.Count.ToGetRequestInformation(), cancellationToken));

    /// <summary>Returns many Zendesk organizations by id.</summary>
    [McpServerTool(Name = "organizations_get_many", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Batch form of organizations_get: many organizations by numeric id in one call, ideal for resolving " +
        "organization_ids collected from tickets or users. Hard cap 100 ids/call (Zendesk's show_many limit); " +
        "for more, split into batches of 100, one call per batch. Rows are lean summaries; detail:'full' for " +
        "complete records, or organizations_get for one.")]
    public Task<JsonElement> ReadMany(
        [Description("Numeric Zendesk organization ids (at most 100 per call).")]
        long[] ids,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // show_many rejects more than 100 ids with 400 Bad Request — surface the API contract as an
            // actionable batching instruction instead of silently fanning out server-side.
            if (ids.Length > MaxIdsPerShowManyRequest)
                throw new McpException(
                    $"organizations_get_many accepts at most {MaxIdsPerShowManyRequest} ids per call (Zendesk's " +
                    $"show_many limit) but was passed {ids.Length}. Split the ids into batches of " +
                    $"{MaxIdsPerShowManyRequest} and call once per batch.");
            if (ids.Length == 0)
                return ZendeskLean.BuildOffsetListEnvelope(EmptyOrganizationsResponse(), "organizations",
                    null, parsedDetail, MaxResponseChars("organizations_get_many"));

            var request = zendesk.Api.V2.Organizations.Show_many.ToGetRequestInformation(cfg =>
                cfg.QueryParameters.Ids = string.Join(',', ids));
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "organizations", null, parsedDetail,
                MaxResponseChars("organizations_get_many"));
        });

    /// <summary>Looks up organizations by exact name or external id.</summary>
    [McpServerTool(Name = "organizations_get_by_name_or_external_id", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lookup organizations by EXACT (case-insensitive) name or by external id. Provide exactly one of 'name' " +
        "or 'externalId' — exact-match lookup, not search syntax. For prefix matching use " +
        "organizations_autocomplete. Rows are lean summaries; detail:'full' for complete records, or " +
        "organizations_get for one.")]
    public Task<JsonElement> Search(
        [Description("Exact organization name (case-insensitive). Mutually exclusive with externalId.")]
        string? name = null,
        [Description("Organization's external id. Mutually exclusive with name.")]
        string? externalId = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // The endpoint is an exact-match lookup on ONE of the two attributes — Zendesk rejects both/neither.
            if (string.IsNullOrWhiteSpace(name) == string.IsNullOrWhiteSpace(externalId))
                throw new ArgumentException("Provide exactly one of name or externalId.", nameof(name));

            var request = zendesk.Api.V2.Organizations.Search.ToGetRequestInformation(cfg =>
            {
                if (!string.IsNullOrWhiteSpace(name)) cfg.QueryParameters.Name = name;
            });
            // Spec gap: the generated builder mistypes external_id as an integer; external ids are opaque strings.
            // The template already declares the parameter, so only the raw value needs to be supplied.
            if (!string.IsNullOrWhiteSpace(externalId)) request.QueryParameters.Add("external_id", externalId);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "organizations", null, parsedDetail,
                MaxResponseChars("organizations_get_by_name_or_external_id"));
        });

    /// <summary>Suggests organizations whose name starts with a prefix.</summary>
    [McpServerTool(Name = "organizations_autocomplete", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Suggest organizations whose name STARTS WITH a prefix — use when the exact name is unknown " +
        "(organizations_get_by_name_or_external_id requires an exact match). Rows are lean summaries (default " +
        "perPage 10); detail:'full' for complete records, or organizations_get for one. Throttled separately " +
        "from the normal account limit (docs call out 429s on rapid repeated calls) — space out calls and honor " +
        "the Retry-After hint instead of retrying immediately. Offset pagination: total in 'count'; " +
        "'has_more'/'next_page' drive paging.")]
    public Task<JsonElement> Autocomplete(
        [Description(
            "Name prefix to match — returns organizations whose name STARTS WITH this value. Required.")]
        string name,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 10, max 100). Total in 'count'; 'has_more'/'next_page' drive paging.")]
        int? perPage = 10,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            // Spec gap: offset paging parameters are missing from the generated builder. The template carries a
            // literal '?name={name}' plus an '{&...}' continuation, which WithQuery splices into correctly.
            var request = zendesk.Api.V2.Organizations.Autocomplete.ToGetRequestInformation(cfg =>
                    cfg.QueryParameters.Name = name)
                .WithQuery("page", page)
                .WithQuery("per_page", perPage);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "organizations", page, parsedDetail,
                MaxResponseChars("organizations_autocomplete"));
        });

    /// <summary>Lists the users of an organization.</summary>
    [McpServerTool(Name = "organizations_users_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Users (end users and agents) attached to an organization, as lean user summary rows (id, name, email, " +
        "role, active, suspended, organization_id, phone, last_login_at, external_id; default perPage 25). " +
        "detail:'full' for complete user records, or users_get for one. For just the member COUNT call " +
        "organizations_users_count. Offset pagination: total in 'count'; 'has_more'/'next_page' drive paging.")]
    public Task<JsonElement> Users(
        [Description("Numeric Zendesk organization id.")]
        long organizationId,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100; larger capped at 100). Total in 'count'; " +
            "'has_more'/'next_page' drive paging.")]
        int? perPage = 25,
        [Description(
            "Sideloads resolving ids inline in one call: any of \"organizations\", \"groups\", \"identities\". " +
            "Returned as sibling arrays.")]
        string[]? include = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // Spec gap: the generated builder has paging but no `include` sideload parameter.
            var request = zendesk.Api.V2.Organizations[organizationId].Users.ToGetRequestInformation(cfg =>
            {
                cfg.QueryParameters.Page = page;
                cfg.QueryParameters.PerPage = perPage;
            }).WithInclude(JoinInclude(include));
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "users", page, parsedDetail,
                MaxResponseChars("organizations_users_list"));
        });

    /// <summary>Returns the approximate user count of an organization.</summary>
    [McpServerTool(Name = "organizations_users_count", ReadOnly = true, OpenWorld = false)]
    [Description(
        "User count for an organization — cheaper than paging organizations_users_list to size the membership. " +
        "When count exceeds 100,000 it refreshes only every 24h and 'value' is capped at 100,000 until the " +
        "background update completes ('refreshed_at' = cache time, may be null during that window). For an exact " +
        "filtered count use search_count (e.g. \"type:user organization:12345\").")]
    public Task<JsonElement> UsersCount(
        [Description("Numeric Zendesk organization id.")]
        long organizationId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => requestAdapter.SendForJsonAsync(
            zendesk.Api.V2.Organizations[organizationId].Users.Count.ToGetRequestInformation(),
            cancellationToken));

    /// <summary>Lists an organization's memberships.</summary>
    [McpServerTool(Name = "organizations_memberships_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "User-to-organization links for an organization, each carrying its membership id (needed to delete a " +
        "link) and whether it is the user's default organization. Rows already lean (API self-links and null " +
        "fields omitted; default perPage 25); detail:'full' returns the same records. For full user records use " +
        "organizations_users_list. Offset pagination: total in 'count'; 'has_more'/'next_page' drive paging.")]
    public Task<JsonElement> Memberships(
        [Description("Numeric Zendesk organization id.")]
        long organizationId,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100; larger capped at 100). Total in 'count'; " +
            "'has_more'/'next_page' drive paging.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // Spec gap: the generated builder exposes no paging parameters on this endpoint.
            var request = zendesk.Api.V2.Organizations[organizationId].Organization_memberships
                .ToGetRequestInformation()
                .WithQuery("page", page)
                .WithQuery("per_page", perPage);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return BuildMembershipsEnvelope(json, page, parsedDetail,
                MaxResponseChars("organizations_memberships_list"));
        });

    /// <summary>Returns an organization merge job's status.</summary>
    [McpServerTool(Name = "organizations_merges_get", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Status of an organization merge — poll after organizations_merge until 'status' is \"complete\". " +
        "status: new|in progress|error|complete; on \"error\" retry by repeating organizations_merge. The id is " +
        "the merge's own string id, not a job_status id — job_statuses_get does not track organization merges.")]
    public Task<JsonElement> MergeStatus(
        [Description(
            "Organization merge id returned by organizations_merge — opaque string (e.g. " +
            "\"01HPZM6206BF4G63783E5349AD\"), not a numeric job_status id.")]
        string mergeId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(mergeId);
            return requestAdapter.SendForJsonAsync(
                zendesk.Api.V2.Organization_merges[mergeId].ToGetRequestInformation(), cancellationToken);
        });

    /// <summary>Lists an organization's tags.</summary>
    [McpServerTool(Name = "organizations_tags_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Tags set on an organization (requires organization tagging enabled in Support). Tags are changed via " +
        "organizations_update by sending the full replacement list.")]
    public Task<JsonElement> Tags(
        [Description("Numeric Zendesk organization id.")]
        long organizationId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => requestAdapter.SendForJsonAsync(
            zendesk.Api.V2.Organizations[organizationId].Tags.ToGetRequestInformation(), cancellationToken));

    /// <summary>Resolves the response-size budget for a tool (see <see cref="McpToolsOptions.GetMaxResponseChars" />).</summary>
    private int MaxResponseChars(string toolName) => mcpOptions.CurrentValue.Tools.GetMaxResponseChars(toolName);

    /// <summary>Builds the flat sideload value (e.g. <c>users,groups</c>), or <c>null</c> when nothing is requested.</summary>
    private static string? JoinInclude(string[]? include) =>
        include is null || include.Length == 0 ? null : string.Join(',', include);

    /// <summary>An empty wire-shaped organizations response, for the no-ids fast path of <c>organizations_get_many</c>.</summary>
    private static JsonElement EmptyOrganizationsResponse() =>
        JsonSerializer.SerializeToElement(new JsonObject { ["organizations"] = new JsonArray() });

    /// <summary>
    ///     Builds the lean list envelope for the memberships sublist. <see cref="ZendeskLean" /> registers no
    ///     summary shape for <c>organization_memberships</c> (the rows are tiny — there is nothing to strip
    ///     beyond what the full view already drops), so BOTH detail levels project the rows through the full
    ///     view (API self-links and null-valued fields omitted) and only the envelope's <c>detail</c> echo
    ///     differs. Summary mode still owes the envelope's sideload contract, though — any sideloaded array is
    ///     summary-projected (or omitted with a note) via
    ///     <see cref="ZendeskLean.ApplySummarySideloadContract" /> before the full-mode assembly, so a raw
    ///     Zendesk sideload can never ride a summary response in full view.
    /// </summary>
    private static JsonElement BuildMembershipsEnvelope(JsonElement response, int? requestPage,
        ZendeskDetail detail, int maxResponseChars)
    {
        string? note = null;
        if (detail is ZendeskDetail.Summary && response.ValueKind is JsonValueKind.Object &&
            JsonNode.Parse(response.GetRawText()) is JsonObject source)
        {
            note = ZendeskLean.ApplySummarySideloadContract(source, "organization_memberships");
            response = JsonSerializer.SerializeToElement(source);
        }

        var envelope = ZendeskLean.BuildOffsetListEnvelope(response, "organization_memberships", requestPage,
            ZendeskDetail.Full, maxResponseChars, note);
        if (detail is ZendeskDetail.Full) return envelope;

        // Rewrite the detail echo to the requested level; replacing the existing key preserves its position,
        // keeping the envelope metadata-first.
        var node = (JsonObject)JsonNode.Parse(envelope.GetRawText())!;
        node["detail"] = "summary";
        return JsonSerializer.SerializeToElement(node);
    }
}