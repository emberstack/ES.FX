using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk <c>groups</c> resource — resolves the <c>group_id</c> carried on tickets and
///     organizations, and enumerates the agents assignable within a group.
/// </summary>
public interface IZendeskGroupsApi
{
    /// <summary>Lists groups (<c>GET /api/v2/groups.json</c>).</summary>
    Task<ZendeskGroupsResult> ListAsync(int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a group by id (<c>GET /api/v2/groups/{id}.json</c>).</summary>
    Task<ZendeskGroup> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists the memberships (agents) of a group (<c>GET /api/v2/groups/{id}/memberships.json</c>) — the set of
    ///     agents a ticket routed to this group can be assigned to.
    /// </summary>
    Task<ZendeskGroupMembershipsResult> GetMembershipsAsync(long groupId, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default);
}