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
        [Description(
            "The Zendesk search query. Filter the resource type with type: (allowed values: ticket, user, " +
            "organization, group). Comparison operators: ':' (equals), '<', '>', '<=', '>='; prefix a term with " +
            "'-' to exclude and use '*' as a wildcard. Dates use YYYY-MM-DD (ISO 8601 date-time also supported, " +
            "e.g. created>2015-09-01T12:00:00-08:00) plus relative shortcuts like created>4hours (units: minutes, " +
            "hours, days, weeks, months, years). Limited to 64 words. Example: \"type:ticket status:open " +
            "tags:vip\".")]
        string query,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Search.CountAsync(query, cancellationToken: cancellationToken));
}
