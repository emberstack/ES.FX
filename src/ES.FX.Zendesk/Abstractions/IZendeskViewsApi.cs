using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk <c>views</c> resource — saved, shared ticket filters and their contents.
/// </summary>
public interface IZendeskViewsApi
{
    /// <summary>Lists views (<c>GET /api/v2/views.json</c>), optionally filtered to active views only.</summary>
    Task<ZendeskViewsResult> ListAsync(bool? active = null, int? pageSize = null, string? afterCursor = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a view by id (<c>GET /api/v2/views/{id}.json</c>).</summary>
    Task<ZendeskView> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the tickets currently matching a view (<c>GET /api/v2/views/{id}/tickets.json</c>).
    ///     <paramref name="include" /> sideloads (<c>users</c>, <c>groups</c>, <c>organizations</c>) resolve
    ///     related records inline as sibling arrays.
    /// </summary>
    Task<ZendeskTicketsResult> GetTicketsAsync(long viewId, int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the (cached) ticket count of a view (<c>GET /api/v2/views/{id}/count.json</c>). Counts for
    ///     large views are approximate — check <see cref="ZendeskViewCount.Fresh" />.
    /// </summary>
    Task<ZendeskViewCount> GetTicketCountAsync(long viewId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a view (<c>POST /api/v2/views.json</c>). <c>Title</c> and at least one <c>All</c> condition on
    ///     status/type/group/assignee/requester are required.
    /// </summary>
    Task<ZendeskView> CreateAsync(ZendeskViewWrite view, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates a view (<c>PUT /api/v2/views/{id}.json</c>). WARNING: condition arrays are replaced wholesale —
    ///     send the complete <c>All</c>/<c>Any</c> sets when touching any condition.
    /// </summary>
    Task<ZendeskView> UpdateAsync(long id, ZendeskViewWrite view, CancellationToken cancellationToken = default);

    /// <summary>Deletes a view (<c>DELETE /api/v2/views/{id}.json</c>).</summary>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}