using System.ComponentModel;
using System.Text.Json;
using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.Support;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP read tools for Zendesk views (saved, shared ticket filters). Namespaced <c>views_*</c>.
/// </summary>
/// <remarks>
///     Requests are built from the generated request builders but sent through
///     <see cref="ZendeskKiotaRequests.SendForJsonAsync" /> (raw JSON passthrough) instead of the typed models:
///     the generated view models mark the fields that matter here as read-only and drop them on re-serialization
///     (<c>ViewObject</c> loses <c>id</c>/<c>created_at</c>/<c>updated_at</c>/<c>default</c>,
///     <c>ViewCountObject</c> serializes nothing at all, and the list/tickets envelopes lose the offset-pagination
///     fields <c>count</c>/<c>next_page</c>/<c>previous_page</c> their tool descriptions promise). The escape
///     hatch also supplies the cursor-pagination and paging query parameters the published spec omits.
///     List responses are then projected through <see cref="ZendeskLean" /> into the uniform lean envelope —
///     summary rows by default, complete records via <c>detail:'full'</c> or the <c>*_get</c> tools.
/// </remarks>
[McpServerToolType]
public sealed class ZendeskViewTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IOptionsMonitor<McpOptions> mcpOptions)
{
    /// <summary>The uniform <c>detail</c> parameter description shared by the list tools.</summary>
    private const string DetailDescription =
        "Row detail: 'summary' (default, lean triage rows) | 'full' (complete records minus null fields and API " +
        "links).";

    /// <summary>Lists Zendesk views.</summary>
    [McpServerTool(Name = "views_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Zendesk views (saved, shared ticket filters) as lean summary rows: id, title, active, default, " +
        "position. Conditions/execution layout/restriction omitted — views_get (or detail:'full') for a view's " +
        "filter rules. views_tickets_list for the tickets a view matches; views_count for just the number. " +
        "Cursor pagination: pageSize default 25 (max 100); response has_more/after_cursor drive continuation.")]
    public Task<JsonElement> List(
        [Description("true=active only; false=inactive only; omit=both (optional).")]
        bool? active = null,
        [Description("Results per page (default 25, endpoint max 100).")]
        int? pageSize = 25,
        [Description(
            "Continuation cursor for the NEXT page — OMIT for first page. Must be the opaque token copied " +
            "verbatim from previous response's after_cursor; not a page number, don't guess or pass empty " +
            "(invalid cursor → 400).")]
        string? afterCursor = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var request = zendesk.Api.V2.Views
                .ToGetRequestInformation(configuration => configuration.QueryParameters.Active = active)
                .WithCursorPagination(pageSize, afterCursor);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildCursorListEnvelope(json, "views", parsedDetail,
                MaxResponseChars("views_list"));
        });

    /// <summary>Returns a Zendesk view by id.</summary>
    [McpServerTool(Name = "views_get", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Single Zendesk view by id, including conditions (all/any filter rules) and execution (columns, " +
        "sorting/grouping) — the detail sink for views_list rows. Null fields and API self-links omitted; absent " +
        "field means null/empty.")]
    public Task<JsonElement> Read(
        [Description("Numeric Zendesk view id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var request = zendesk.Api.V2.Views[id].ToGetRequestInformation();
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("view", out var view)
                ? ZendeskLean.EnsureWithinBudget(ZendeskLean.ToFullView(view), "views_get",
                    MaxResponseChars("views_get"),
                    "View too large — inspect its conditions/columns in smaller pieces or via views_list summary.")
                : throw new McpException($"Zendesk view '{id}' was not found.");
        });

    /// <summary>Returns the tickets currently matching a view.</summary>
    [McpServerTool(Name = "views_tickets_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Tickets currently matching a view as lean ticket summary rows: id, subject, 150-char description " +
        "excerpt, status, priority, dates, people ids, tags. detail:'full' for complete records, or tickets_get " +
        "for one. views_count for just the NUMBER of matching tickets. perPage default 25 (max 100); total in " +
        "'count'; 'has_more'/'next_page' drive paging.")]
    public Task<JsonElement> Tickets(
        [Description("Numeric Zendesk view id.")]
        long viewId,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description("Results per page (default 25, max 100).")]
        int? perPage = 25,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // The offset paging pair is documented for this endpoint but never OAS-modeled (spec-anomaly ledger,
            // src/ES.FX.Zendesk/OpenApi/README.md). No include parameter is offered: sideloads are neither
            // OAS-modeled nor documented for this route, and remain unverified against a live tenant.
            var request = zendesk.Api.V2.Views[viewId].Tickets.ToGetRequestInformation()
                .WithQuery("page", page)
                .WithQuery("per_page", perPage);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "tickets", page, parsedDetail,
                MaxResponseChars("views_tickets_list"));
        });

    /// <summary>Returns the (cached) ticket count of a view.</summary>
    [McpServerTool(Name = "views_count", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Cached ticket count of a view — cheaper than listing its tickets. Large views: approximate, cached " +
        "60-90 min; 'value' may be null while data reloads; 'fresh' is false when cached value is stale. Rate " +
        "limited 5 requests/min per view per agent.")]
    public Task<JsonElement> Count(
        [Description("Numeric Zendesk view id.")]
        long viewId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var request = zendesk.Api.V2.Views[viewId].Count.ToGetRequestInformation();
            return await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
        });

    /// <summary>Runs a view as a work queue, returning its rows with the configured columns.</summary>
    [McpServerTool(Name = "views_rows_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Run a view as an agent WORK QUEUE — lists its rows rendered with the view's configured columns and " +
        "sorting (what an agent sees opening the view), not just raw tickets (that's views_tickets_list). Response " +
        "carries 'columns' (the layout) and 'items' (rows: scalar column values + a lean ticket summary each; " +
        "detail:'full' for complete tickets). Ordered by the view's own sort unless overridden. Fixed page size; " +
        "'has_more'/'next_page' drive paging. views_count_many for just the queue sizes.")]
    public Task<JsonElement> Execute(
        [Description("Numeric Zendesk view id.")]
        long viewId,
        [Description(
            "Override sort column (e.g. \"status\", \"priority\", \"created_at\", \"updated_at\"). OMIT to keep the view's own sort.")]
        string? sortBy = null,
        [Description("asc|desc. OMIT to keep the view's own order.")]
        string? sortOrder = null,
        [Description("1-based page number (optional).")]
        int? page = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var request = zendesk.Api.V2.Views[viewId].Execute.ToGetRequestInformation(configuration =>
            {
                if (!string.IsNullOrWhiteSpace(sortBy)) configuration.QueryParameters.SortBy = sortBy;
                if (!string.IsNullOrWhiteSpace(sortOrder)) configuration.QueryParameters.SortOrder = sortOrder;
                configuration.QueryParameters.Page = page;
            });
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildViewExecuteEnvelope(json, page, parsedDetail, MaxResponseChars("views_rows_list"));
        });

    /// <summary>Returns cached ticket counts for several views at once.</summary>
    [McpServerTool(Name = "views_count_many", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Cached ticket counts for MULTIPLE views in one call — bulk backlog/queue sizing without listing tickets. " +
        "Max 20 view ids per call. Returns view_counts: [{view_id, value, pretty, fresh, ...}]. Large views: value " +
        "is approximate and cached; 'fresh' is false while the cached value reloads. Rate limited. Use views_count " +
        "for a single view.")]
    public Task<JsonElement> CountMany(
        [Description("View ids to count, e.g. [25, 26]. At least one required, at most 20.")]
        long[] ids,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            if (ids is null || ids.Length == 0) throw new McpException("Provide at least one view id.");
            if (ids.Length > 20) throw new McpException("views_count_many accepts at most 20 view ids per call.");
            var request = zendesk.Api.V2.Views.Count_many.ToGetRequestInformation(configuration =>
                configuration.QueryParameters.Ids = string.Join(',', ids));
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            // Strip API self-links; the payload is small (one row per requested view).
            return ZendeskLean.ToFullView(json);
        });

    /// <summary>Resolves the response-size budget for a tool (see <see cref="McpToolsOptions.GetMaxResponseChars" />).</summary>
    private int MaxResponseChars(string toolName) => mcpOptions.CurrentValue.Tools.GetMaxResponseChars(toolName);
}