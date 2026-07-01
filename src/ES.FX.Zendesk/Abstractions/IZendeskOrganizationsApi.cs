using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk <c>organizations</c> resource.
/// </summary>
public interface IZendeskOrganizationsApi
{
    /// <summary>Returns an organization by id (<c>GET /api/v2/organizations/{id}.json</c>).</summary>
    Task<ZendeskOrganization> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the tickets belonging to an organization (<c>GET /api/v2/organizations/{id}/tickets.json</c>).
    ///     <paramref name="include" /> sideloads (<c>users</c>, <c>groups</c>, <c>organizations</c>) resolve related
    ///     records inline as sibling arrays on the result.
    /// </summary>
    Task<ZendeskTicketsResult> GetTicketsAsync(long organizationId, int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default);
}