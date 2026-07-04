using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk <c>groups</c> resource — resolves the <c>group_id</c> carried on tickets and
///     organizations, and enumerates the agents assignable within a group.
/// </summary>
public interface IZendeskGroupsApi
{
    /// <summary>
    ///     Lists groups (<c>GET /api/v2/groups.json</c>). <paramref name="include" /> sideloads (<c>users</c>)
    ///     resolve the groups' members inline.
    /// </summary>
    Task<ZendeskGroupsResult> ListAsync(int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default);

    /// <summary>Returns a group by id (<c>GET /api/v2/groups/{id}.json</c>).</summary>
    Task<ZendeskGroup> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists the memberships (agents) of a group (<c>GET /api/v2/groups/{id}/memberships.json</c>) — the set of
    ///     agents a ticket routed to this group can be assigned to. <paramref name="include" /> sideloads
    ///     (<c>users</c>, <c>groups</c>) resolve the referenced records inline.
    /// </summary>
    Task<ZendeskGroupMembershipsResult> GetMembershipsAsync(long groupId, int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists the groups assignable to tickets for the current agent context
    ///     (<c>GET /api/v2/groups/assignable.json</c>).
    /// </summary>
    Task<ZendeskGroupsResult> GetAssignableAsync(int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the (cached, approximate) group count (<c>GET /api/v2/groups/count.json</c>).
    /// </summary>
    Task<ZendeskCount> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists the users of a group (<c>GET /api/v2/groups/{id}/users.json</c>).
    /// </summary>
    Task<ZendeskUsersResult> GetUsersAsync(long groupId, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a group (<c>POST /api/v2/groups.json</c>). Decide <c>IsPublic</c> at creation — a private
    ///     group can never be made public later.
    /// </summary>
    Task<ZendeskGroup> CreateAsync(ZendeskGroupWrite group, CancellationToken cancellationToken = default);

    /// <summary>Updates a group (<c>PUT /api/v2/groups/{id}.json</c>).</summary>
    Task<ZendeskGroup> UpdateAsync(long id, ZendeskGroupWrite group, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a group (<c>DELETE /api/v2/groups/{id}.json</c>).</summary>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Assigns an agent to a group (<c>POST /api/v2/group_memberships.json</c>). Set
    ///     <paramref name="default" /> so tickets assigned directly to the agent assume this group.
    /// </summary>
    Task<ZendeskGroupMembership> CreateMembershipAsync(long userId, long groupId, bool? @default = null,
        CancellationToken cancellationToken = default);

    /// <summary>Assigns up to 100 agents to groups as an async job (<c>POST .../group_memberships/create_many.json</c>).</summary>
    Task<ZendeskJobStatus> CreateManyMembershipsAsync(IReadOnlyList<ZendeskGroupMembership> memberships,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes a group membership by its MEMBERSHIP id (<c>DELETE /api/v2/group_memberships/{id}.json</c>).
    ///     Side effect: Zendesk schedules a job un-assigning the agent's working tickets in that group.
    /// </summary>
    Task DeleteMembershipAsync(long membershipId, CancellationToken cancellationToken = default);

    /// <summary>Removes up to 100 group memberships as an async job (<c>DELETE .../destroy_many.json</c>).</summary>
    Task<ZendeskJobStatus> DeleteManyMembershipsAsync(IReadOnlyList<long> membershipIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Makes a group membership the agent's default
    ///     (<c>PUT /api/v2/users/{userId}/group_memberships/{id}/make_default.json</c>). Returns the user's FULL
    ///     group membership list.
    /// </summary>
    Task<ZendeskGroupMembershipsResult> MakeMembershipDefaultAsync(long userId, long membershipId,
        CancellationToken cancellationToken = default);
}