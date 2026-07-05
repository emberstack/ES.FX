using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP read tools for Zendesk views (saved, shared ticket filters). Namespaced <c>views_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskViewTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Lists Zendesk views.</summary>
    [McpServerTool(Name = "views_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists Zendesk views (saved, shared ticket filters), optionally only active ones. Use " +
        "views_tickets_list to see the tickets a view currently matches. Cursor pagination: pass " +
        "pageSize/afterCursor; the result's meta.has_more/meta.after_cursor drive continuation. Read-only.")]
    public Task<ZendeskViewsResult> List(
        [Description("When true, only active views; when false, only inactive views; omit to return both (optional).")]
        bool? active = null,
        [Description("The cursor page size; the endpoint returns at most 100 records per page (optional).")]
        int? pageSize = null,
        [Description("The cursor from the previous page's meta.after_cursor (omit for the first page).")]
        string? afterCursor = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Views.ListAsync(active: active, pageSize: pageSize, afterCursor: afterCursor,
                cancellationToken: cancellationToken));

    /// <summary>Returns a Zendesk view by id.</summary>
    [McpServerTool(Name = "views_get", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns a single Zendesk view by id, including its conditions (the all/any filter rules) and execution " +
        "(columns, sorting/grouping). Read-only.")]
    public Task<ZendeskView> Read(
        [Description("The numeric Zendesk view id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Views.GetByIdAsync(id, cancellationToken));

    /// <summary>Returns the tickets currently matching a view.</summary>
    [McpServerTool(Name = "views_tickets_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the tickets currently matching a view. Offset pagination: count/next_page indicate more pages. " +
        "Sideload related records with include (users, groups, organizations) to resolve ids inline. Read-only.")]
    public Task<ZendeskTicketsResult> Tickets(
        [Description("The numeric Zendesk view id.")]
        long viewId,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Offset pagination; results per page (optional, max 100). Read the total from 'count' and continue while " +
            "'next_page' is non-null.")]
        int? perPage = null,
        [Description(
            "Sideload names to resolve related record ids inline; supported values include users, groups, " +
            "organizations (optional).")]
        string[]? include = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Views.GetTicketsAsync(viewId, page: page, perPage: perPage, include: include,
                cancellationToken: cancellationToken));

    /// <summary>Returns the (cached) ticket count of a view.</summary>
    [McpServerTool(Name = "views_count", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the (cached) ticket count of a view — cheaper than listing its tickets. Counts for large views " +
        "are approximate and can be cached for 60-90 minutes; 'value' may be null while the data reloads, and " +
        "'fresh' is false when the cached value is stale. Rate limited to 5 requests per minute, per view, per " +
        "agent. Read-only.")]
    public Task<ZendeskViewCount> Count(
        [Description("The numeric Zendesk view id.")]
        long viewId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Views.GetTicketCountAsync(viewId, cancellationToken));
}
