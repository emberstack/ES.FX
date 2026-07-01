using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk <c>users</c> resource.
/// </summary>
public interface IZendeskUsersApi
{
    /// <summary>
    ///     Returns the user associated with the configured credentials (<c>GET /api/v2/users/me.json</c>).
    /// </summary>
    Task<ZendeskUser> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a user by id (<c>GET /api/v2/users/{id}.json</c>).
    /// </summary>
    Task<ZendeskUser> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Searches users (<c>GET /api/v2/users/search.json</c>). The query matches name, email, phone, external id,
    ///     etc.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="perPage">The number of results per page (Zendesk caps at 100).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<ZendeskUsersResult> SearchAsync(string query, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns many users by id in a single request (<c>GET /api/v2/users/show_many.json?ids=</c>), for
    ///     resolving the author/assignee/requester/CC ids on tickets, comments, and audits without one call per id.
    ///     Zendesk accepts up to 100 ids per request; an empty list returns an empty result without a call.
    /// </summary>
    Task<ZendeskUsersResult> GetManyAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the tickets requested by a user — their ticket history
    ///     (<c>GET /api/v2/users/{id}/tickets/requested.json</c>). <paramref name="include" /> sideloads
    ///     (<c>users</c>, <c>groups</c>, <c>organizations</c>) resolve related records inline as sibling arrays.
    /// </summary>
    Task<ZendeskTicketsResult> GetRequestedTicketsAsync(long userId, int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default);
}