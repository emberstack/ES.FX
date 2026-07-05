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
///     MCP tools for Zendesk groups (agent teams tickets are routed to). Namespaced <c>groups_*</c>.
/// </summary>
/// <remarks>
///     Requests are built through the generated request builders, but responses are returned as the raw wire
///     JSON (<see cref="ZendeskKiotaRequests.SendForJsonAsync" />) rather than round-tripped through the
///     generated models: the published spec marks server-assigned fields (<c>id</c>, <c>created_at</c>,
///     <c>updated_at</c>, <c>url</c>, the list envelopes' <c>count</c>/<c>next_page</c>, ...) as read-only,
///     so Kiota's serializer would silently drop them from the tool result. List responses are then projected
///     through <see cref="ZendeskLean" /> into the uniform lean envelope — summary rows by default, complete
///     records via <c>detail:'full'</c> or <c>groups_get</c>.
/// </remarks>
[McpServerToolType]
public sealed class ZendeskGroupTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IOptionsMonitor<McpOptions> mcpOptions)
{
    /// <summary>The uniform <c>detail</c> parameter description shared by the list tools.</summary>
    private const string DetailDescription =
        "Row detail: 'summary' (default, lean triage rows) | 'full' (complete records, null fields & API links " +
        "omitted).";

    /// <summary>Lists Zendesk groups.</summary>
    [McpServerTool(Name = "groups_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Zendesk groups (agent teams) as summary rows (id, name, description, default, deleted, is_public); " +
        "detail:'full' for complete records, groups_get for one. Resolves a ticket's/organization's numeric " +
        "group_id to a name. Default perPage 25 (max 100). CAUTION: \"users\" sideload returns EVERY member of " +
        "EVERY group on the page (summary-projected), can dwarf the rows — for one group's members use " +
        "groups_users_list.")]
    public Task<JsonElement> List(
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Per page (default 25, max 100). Total in 'count'; 'has_more'/'next_page' drive paging.")]
        int? perPage = 25,
        [Description(
            "Sideloads as summary-projected sibling arrays. \"users\": each group's members same roundtrip — for " +
            "EVERY group on the page, can be very large; prefer groups_users_list for one group. " +
            "\"group_settings\": no summary shape, only returned with detail:'full'. Unknown values silently " +
            "ignored.")]
        string[]? include = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var request = zendesk.Api.V2.Groups.ToGetRequestInformation(cfg =>
            {
                cfg.QueryParameters.Page = page;
                cfg.QueryParameters.PerPage = perPage;
                cfg.QueryParameters.Include = JoinInclude(include);
            });
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "groups", page, parsedDetail,
                MaxResponseChars("groups_list"));
        });

    /// <summary>Returns a Zendesk group by id.</summary>
    [McpServerTool(Name = "groups_get", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Single Zendesk group by id — full-detail record (null fields & API self-links omitted; absent field " +
        "means null/empty): name/description behind a ticket's/organization's group_id, plus default/deleted/" +
        "is_public flags and timestamps.")]
    public Task<JsonElement> Read(
        [Description("Numeric Zendesk group id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var request = zendesk.Api.V2.Groups[id].ToGetRequestInformation();
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("group", out var group)
                ? ZendeskLean.EnsureWithinBudget(ZendeskLean.ToFullView(group), "groups_get",
                    MaxResponseChars("groups_get"), "Record exceeds the response budget.")
                : throw new McpException($"Zendesk group '{id}' was not found.");
        });

    /// <summary>Lists the agents that belong to a group.</summary>
    [McpServerTool(Name = "groups_memberships_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Memberships (agents) of a group — agents a ticket routed here can be assigned to. Summary rows (id, " +
        "user_id, group_id, default, created_at, updated_at); 'default' marks the agent's default group. Default " +
        "perPage 25 (max 100); detail:'full' for complete records. Prefer \"users\" sideload (include) to " +
        "resolve agent names inline vs following up with users_get_many; for just the count use " +
        "groups_users_count.")]
    public Task<JsonElement> Memberships(
        [Description("Numeric Zendesk group id.")]
        long groupId,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Per page (default 25, max 100). Total in 'count'; 'has_more'/'next_page' drive paging.")]
        int? perPage = 25,
        [Description(
            "Sideloads to resolve ids inline; valid: \"users\", \"groups\". Returned as summary-projected " +
            "sibling arrays.")]
        string[]? include = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // The generated builder for /groups/{group_id}/memberships exposes no query parameters, although the
            // live API accepts offset pagination and sideloads there — a recorded spec anomaly (see the ledger
            // in src/ES.FX.Zendesk/OpenApi/README.md); extend the generated request instead.
            var request = zendesk.Api.V2.Groups[groupId].Memberships.ToGetRequestInformation()
                .WithQuery("page", page)
                .WithQuery("per_page", perPage)
                .WithInclude(JoinInclude(include));
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return BuildMembershipsEnvelope(json, page, parsedDetail,
                MaxResponseChars("groups_memberships_list"));
        });

    /// <summary>Lists the groups assignable to tickets for the current agent.</summary>
    [McpServerTool(Name = "groups_assignable_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Zendesk groups assignable to tickets for the authenticated agent, as summary rows (id, name, " +
        "description, default, deleted, is_public) — pick a valid group_id when routing a ticket vs guessing " +
        "from groups_list. Default perPage 25 (max 100); detail:'full' for complete records, groups_get for " +
        "one.")]
    public Task<JsonElement> Assignable(
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Per page (default 25, max 100). Total in 'count'; 'has_more'/'next_page' drive paging.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // The generated builder for /groups/assignable exposes no query parameters, although the live API
            // accepts offset pagination there — extend the generated request instead.
            var request = zendesk.Api.V2.Groups.Assignable.ToGetRequestInformation()
                .WithQuery("page", page)
                .WithQuery("per_page", perPage);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "groups", page, parsedDetail,
                MaxResponseChars("groups_assignable_list"));
        });

    /// <summary>Returns the approximate group count.</summary>
    [McpServerTool(Name = "groups_count", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Number of Zendesk groups. Cached, approximate above 100,000: refreshed only every ~24h and 'value' " +
        "capped at 100,000 until refresh completes ('refreshed_at' is the cache time, may be null while Zendesk " +
        "recomputes).")]
    public Task<JsonElement> Count(CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => requestAdapter.SendForJsonAsync(
            zendesk.Api.V2.Groups.Count.ToGetRequestInformation(), cancellationToken));

    /// <summary>Lists the users of a group.</summary>
    [McpServerTool(Name = "groups_users_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Users (agents) of a Zendesk group as user summary rows (id, name, email, role, active, suspended, " +
        "organization_id, phone, last_login_at, external_id) — one-call alternative to groups_memberships_list " +
        "then users_get_many when only users matter. Default perPage 25 (max 100); detail:'full' for complete " +
        "user records, users_get for one. For just the COUNT use groups_users_count — don't page these rows.")]
    public Task<JsonElement> Users(
        [Description("Numeric Zendesk group id.")]
        long groupId,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Per page (default 25, max 100). Total in 'count'; 'has_more'/'next_page' drive paging.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // The generated builder for /groups/{group_id}/users lacks the offset pagination parameters the live
            // API accepts (cursor + offset pagination, max 100 per page:
            // https://developer.zendesk.com/api-reference/ticketing/users/users/#list-users-by-group) — extend
            // the generated request instead.
            var request = zendesk.Api.V2.Groups[groupId].Users.ToGetRequestInformation()
                .WithQuery("page", page)
                .WithQuery("per_page", perPage);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "users", page, parsedDetail,
                MaxResponseChars("groups_users_list"));
        });

    /// <summary>Returns the approximate user count of a group.</summary>
    [McpServerTool(Name = "groups_users_count", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Number of users (agents) in a Zendesk group — cheaper than paging groups_users_list to size a team. " +
        "Cached, approximate above 100,000: refreshed only every ~24h and 'value' capped at 100,000 until " +
        "refresh completes ('refreshed_at' is the cache time, may be null while Zendesk recomputes).")]
    public Task<JsonElement> UsersCount(
        [Description("Numeric Zendesk group id.")]
        long groupId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => requestAdapter.SendForJsonAsync(
            zendesk.Api.V2.Groups[groupId].Users.Count.ToGetRequestInformation(), cancellationToken));

    /// <summary>Resolves the response-size budget for a tool (see <see cref="McpToolsOptions.GetMaxResponseChars" />).</summary>
    private int MaxResponseChars(string toolName) => mcpOptions.CurrentValue.Tools.GetMaxResponseChars(toolName);

    /// <summary>
    ///     Builds the lean list envelope for group membership rows. <see cref="ZendeskLean" /> registers no
    ///     <c>group_memberships</c> summary shape, so summary mode is produced here: primary rows are projected
    ///     onto the membership allowlist (<see cref="SummarizeMembership" />), sideloads honor the envelope's
    ///     summary contract via <see cref="ZendeskLean.ApplySummarySideloadContract" /> (a sideload without a
    ///     shape fails visibly — omitted, with the escalation path in the note), and the shared builder then
    ///     assembles the envelope in full mode — a no-op over the pre-projected arrays, so pagination,
    ///     <c>count</c> and the size guard behave exactly as on every other list — before the <c>detail</c>
    ///     label is restored to what the agent asked for. Once a membership summary shape exists in
    ///     <see cref="ZendeskLean" /> this collapses to a single
    ///     <see cref="ZendeskLean.BuildOffsetListEnvelope" /> call.
    /// </summary>
    private static JsonElement BuildMembershipsEnvelope(JsonElement response, int? page, ZendeskDetail detail,
        int maxResponseChars)
    {
        if (detail is ZendeskDetail.Full)
            return ZendeskLean.BuildOffsetListEnvelope(response, "group_memberships", page, detail,
                maxResponseChars);

        if (response.ValueKind is not JsonValueKind.Object)
            throw new McpException("The Zendesk API returned an empty response where a payload was expected.");
        var source = (JsonObject)JsonNode.Parse(response.GetRawText())!;

        if (source["group_memberships"] is JsonArray memberships)
            for (var index = 0; index < memberships.Count; index++)
                if (memberships[index] is JsonObject membership)
                    memberships[index] = SummarizeMembership(membership);

        var note = ZendeskLean.ApplySummarySideloadContract(source, "group_memberships");

        var envelope = ZendeskLean.BuildOffsetListEnvelope(JsonSerializer.SerializeToElement(source),
            "group_memberships", page, ZendeskDetail.Full, maxResponseChars, note);
        var relabeled = (JsonObject)JsonNode.Parse(envelope.GetRawText())!;
        relabeled["detail"] = "summary";
        return JsonSerializer.SerializeToElement(relabeled);
    }

    /// <summary>
    ///     The membership summary allowlist. Memberships carry no heavy fields, so the summary row keeps the
    ///     complete routing identity — the full view differs only in passing unknown wire fields through.
    /// </summary>
    private static JsonObject SummarizeMembership(JsonObject membership)
    {
        var row = new JsonObject();
        Copy(membership, row, "id", "user_id", "group_id", "default", "created_at", "updated_at");
        return row;
    }

    /// <summary>Copies the allowlisted fields that are present and non-null, preserving the given order.</summary>
    private static void Copy(JsonObject source, JsonObject target, params string[] fields)
    {
        foreach (var field in fields)
            if (source[field] is { } value)
                target[field] = value.DeepClone();
    }

    /// <summary>
    ///     Builds the flat comma-separated sideload value (e.g. <c>users,groups</c>), or <c>null</c> when nothing
    ///     is requested — matching the omit-when-empty semantics of the retired client.
    /// </summary>
    private static string? JoinInclude(string[]? include)
    {
        if (include is null || include.Length == 0) return null;
        var joined = string.Join(',', include);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }
}