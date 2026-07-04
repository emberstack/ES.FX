using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk unified search API beyond the ticket-scoped search on
///     <see cref="IZendeskTicketsApi.SearchAsync" />.
/// </summary>
public interface IZendeskSearchApi
{
    /// <summary>
    ///     Returns the number of results a search query matches (<c>GET /api/v2/search/count.json</c>) — a cheap
    ///     way to size a query before paging or exporting. Uses the same query syntax as search.
    /// </summary>
    Task<long> CountAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Exports ticket search results with cursor pagination (<c>GET /api/v2/search/export.json</c>,
    ///     <c>filter[type]=ticket</c>). Unlike search, the export has no 1,000-result cap. Page by passing
    ///     <c>Meta.AfterCursor</c> as <paramref name="afterCursor" /> while <c>Meta.HasMore</c> is <c>true</c>;
    ///     cursors expire after one hour.
    /// </summary>
    /// <param name="query">The Zendesk search query (a <c>type:</c> selector is not needed — the type filter applies).</param>
    /// <param name="pageSize">The cursor page size (Zendesk recommends at most 100).</param>
    /// <param name="afterCursor">The cursor from the previous page's <c>Meta.AfterCursor</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<ZendeskTicketSearchExportResults> ExportTicketsAsync(string query, int? pageSize = null,
        string? afterCursor = null, CancellationToken cancellationToken = default);
}