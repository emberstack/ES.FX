using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for the Zendesk unified search API. Namespaced <c>search_*</c>. The unified count is the only
///     genuinely cross-resource operation kept in the <c>search</c> area; the ticket-scoped deep export lives on
///     <see cref="ZendeskTicketTools" /> as <c>tickets_search_export</c> (its name says <c>tickets</c>, so its area
///     is <c>tickets</c> — keeping the class area-homogeneous for area gating).
/// </summary>
[McpServerToolType]
public sealed class ZendeskSearchTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Returns the number of results a search query matches.</summary>
    [McpServerTool(Name = "search_count", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the number of results a Zendesk search query matches — a cheap way to size a query before paging " +
        "or exporting. Uses the same query syntax as tickets_search. Read-only.")]
    public Task<long> Count(
        [Description("The Zendesk search query (e.g. \"type:ticket status:open tags:vip\").")]
        string query,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Search.CountAsync(query, cancellationToken: cancellationToken));
}
