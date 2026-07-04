using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for the Zendesk unified search API (count and cursor-based ticket export). Namespaced
///     <c>zendesk_search_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskSearchTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Returns the number of results a search query matches.</summary>
    [McpServerTool(Name = "zendesk_search_count", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the number of results a Zendesk search query matches — a cheap way to size a query before paging " +
        "or exporting. Uses the same query syntax as zendesk_tickets_search. Read-only.")]
    public Task<long> Count(
        [Description("The Zendesk search query (e.g. \"type:ticket status:open tags:vip\").")]
        string query,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Search.CountAsync(query, cancellationToken: cancellationToken));

    /// <summary>Exports ticket search results with cursor pagination (no 1,000-result cap).</summary>
    [McpServerTool(Name = "zendesk_search_export_tickets", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Cursor-only deep export of ticket search results. Unlike zendesk_tickets_search there is NO 1,000-result " +
        "cap, so use this for large result sets. A type: selector is not needed — the ticket type filter is applied. " +
        "Cursor pagination: pass pageSize/afterCursor; the result's meta.has_more/meta.after_cursor drive " +
        "continuation. Cursors expire after one hour. Read-only.")]
    public Task<ZendeskTicketSearchExportResults> ExportTickets(
        [Description("The Zendesk search query (the ticket type filter is applied automatically).")]
        string query,
        [Description("The cursor page size (Zendesk recommends at most 100).")]
        int? pageSize = null,
        [Description("The cursor from the previous page's meta.after_cursor (omit for the first page).")]
        string? afterCursor = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Search.ExportTicketsAsync(query, pageSize: pageSize, afterCursor: afterCursor,
                cancellationToken: cancellationToken));
}
