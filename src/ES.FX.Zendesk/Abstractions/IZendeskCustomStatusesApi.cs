using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk <c>custom_statuses</c> resource — decodes the <c>custom_status_id</c>
///     carried on tickets when custom ticket statuses are enabled.
/// </summary>
public interface IZendeskCustomStatusesApi
{
    /// <summary>
    ///     Lists custom ticket statuses (<c>GET /api/v2/custom_statuses.json</c>; not paginated).
    /// </summary>
    /// <param name="active">When set, filters to active (or inactive) statuses.</param>
    /// <param name="default">When set, filters to default (or non-default) statuses.</param>
    /// <param name="statusCategories">
    ///     Comma-separated status categories to filter by — see <see cref="ZendeskStatusCategories" />.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<ZendeskCustomStatusesResult> ListAsync(bool? active = null, bool? @default = null,
        string? statusCategories = null, CancellationToken cancellationToken = default);

    /// <summary>Returns a custom ticket status by id (<c>GET /api/v2/custom_statuses/{id}.json</c>).</summary>
    Task<ZendeskCustomStatus> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a custom ticket status (<c>POST /api/v2/custom_statuses.json</c>; admin-only).
    ///     <c>StatusCategory</c> and <c>AgentLabel</c> are required.
    /// </summary>
    Task<ZendeskCustomStatus> CreateAsync(ZendeskCustomStatusWrite status,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates a custom ticket status (<c>PUT /api/v2/custom_statuses/{id}.json</c>; admin-only).
    ///     <c>StatusCategory</c> cannot be changed; deactivate with <c>Active = false</c>.
    /// </summary>
    Task<ZendeskCustomStatus> UpdateAsync(long id, ZendeskCustomStatusWrite status,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a custom ticket status (<c>DELETE /api/v2/custom_statuses/{id}.json</c>; admin-only). The
    ///     status must first be unassigned from all non-closed tickets, else the delete fails.
    /// </summary>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}