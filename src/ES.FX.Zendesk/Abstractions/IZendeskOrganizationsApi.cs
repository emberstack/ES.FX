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

    /// <summary>
    ///     Lists organizations (<c>GET /api/v2/organizations.json</c>; cursor-paginated — page with
    ///     <c>Meta.AfterCursor</c>).
    /// </summary>
    Task<ZendeskOrganizationsResult> ListAsync(int? pageSize = null, string? afterCursor = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the (cached, approximate) organization count (<c>GET /api/v2/organizations/count.json</c>).
    /// </summary>
    Task<ZendeskCount> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns many organizations by id in a single request
    ///     (<c>GET /api/v2/organizations/show_many.json?ids=</c>). Zendesk accepts up to 100 ids per request;
    ///     larger lists are chunked and merged. An empty list returns an empty result without a call.
    /// </summary>
    Task<ZendeskOrganizationsResult> GetManyAsync(IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Looks up organizations by exact (case-insensitive) name or external id
    ///     (<c>GET /api/v2/organizations/search.json</c>). Provide exactly one of the two parameters — this is
    ///     an exact-match lookup, not search syntax.
    /// </summary>
    Task<ZendeskOrganizationsResult> SearchAsync(string? name = null, string? externalId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Suggests organizations whose name starts with a prefix
    ///     (<c>GET /api/v2/organizations/autocomplete.json?name=</c>; offset-paginated only).
    /// </summary>
    Task<ZendeskOrganizationsResult> AutocompleteAsync(string name, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists the users of an organization (<c>GET /api/v2/organizations/{id}/users.json</c>).
    ///     <paramref name="include" /> sideloads (<c>organizations</c>, <c>groups</c>, <c>identities</c>)
    ///     resolve related records inline.
    /// </summary>
    Task<ZendeskUsersResult> GetUsersAsync(long organizationId, int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists an organization's memberships — the users linked to it
    ///     (<c>GET /api/v2/organizations/{id}/organization_memberships.json</c>).
    /// </summary>
    Task<ZendeskOrganizationMembershipsResult> GetMembershipsAsync(long organizationId, int? page = null,
        int? perPage = null, CancellationToken cancellationToken = default);

    /// <summary>Creates an organization (<c>POST /api/v2/organizations.json</c>). The name must be unique.</summary>
    Task<ZendeskOrganization> CreateAsync(ZendeskOrganizationWrite organization,
        CancellationToken cancellationToken = default);

    /// <summary>Creates up to 100 organizations as an async job (<c>POST /api/v2/organizations/create_many.json</c>).</summary>
    Task<ZendeskJobStatus> CreateManyAsync(IReadOnlyList<ZendeskOrganizationWrite> organizations,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates or updates an organization (<c>POST /api/v2/organizations/create_or_update.json</c>). Matching
    ///     uses <c>Id</c> or <c>ExternalId</c> — NOT the name; an existing name without a matching key errors.
    /// </summary>
    Task<ZendeskOrganization> CreateOrUpdateAsync(ZendeskOrganizationWrite organization,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates an organization (<c>PUT /api/v2/organizations/{id}.json</c>). QUIRK: <c>DomainNames</c>
    ///     overwrites — always send the complete list.
    /// </summary>
    Task<ZendeskOrganization> UpdateAsync(long id, ZendeskOrganizationWrite organization,
        CancellationToken cancellationToken = default);

    /// <summary>Applies the SAME change to up to 100 organizations as an async job (<c>PUT .../update_many.json?ids=</c>).</summary>
    Task<ZendeskJobStatus> UpdateManyAsync(IReadOnlyList<long> ids, ZendeskOrganizationWrite change,
        CancellationToken cancellationToken = default);

    /// <summary>Applies PER-ORGANIZATION changes as an async job (batch form). Every item must carry <c>Id</c>.</summary>
    Task<ZendeskJobStatus> UpdateManyAsync(IReadOnlyList<ZendeskOrganizationWrite> organizations,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes an organization (<c>DELETE /api/v2/organizations/{id}.json</c>).</summary>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Deletes up to 100 organizations as an async job (<c>DELETE .../destroy_many.json</c>).</summary>
    Task<ZendeskJobStatus> DeleteManyAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Merges an organization INTO another (<c>POST /api/v2/organizations/{loserId}/merge.json</c>). The path
    ///     organization is deleted; users, tickets and domain names move to the winner. Async but NOT a
    ///     <c>job_status</c> — poll <see cref="GetMergeAsync" />. Irreversible; admin-only.
    /// </summary>
    Task<ZendeskOrganizationMerge> MergeAsync(long loserOrganizationId, long winnerOrganizationId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns an organization merge job's status (<c>GET /api/v2/organization_merges/{id}.json</c>).</summary>
    Task<ZendeskOrganizationMerge> GetMergeAsync(string mergeId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Links a user to an organization (<c>POST /api/v2/organization_memberships.json</c>). <c>422</c> if the
    ///     membership already exists.
    /// </summary>
    Task<ZendeskOrganizationMembership> CreateMembershipAsync(long userId, long organizationId,
        bool? @default = null, CancellationToken cancellationToken = default);

    /// <summary>Creates up to 100 memberships as an async job (<c>POST .../organization_memberships/create_many.json</c>).</summary>
    Task<ZendeskJobStatus> CreateManyMembershipsAsync(IReadOnlyList<ZendeskOrganizationMembership> memberships,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes a membership (<c>DELETE /api/v2/organization_memberships/{id}.json</c>). Side effect: Zendesk
    ///     schedules a job un-assigning the user's working tickets for that organization.
    /// </summary>
    Task DeleteMembershipAsync(long membershipId, CancellationToken cancellationToken = default);

    /// <summary>Removes up to 100 memberships as an async job (<c>DELETE .../destroy_many.json</c>).</summary>
    Task<ZendeskJobStatus> DeleteManyMembershipsAsync(IReadOnlyList<long> membershipIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Makes a membership the user's default (
    ///     <c>PUT /api/v2/users/{userId}/organization_memberships/{id}/make_default.json</c>).
    ///     Returns the user's FULL membership list.
    /// </summary>
    Task<ZendeskOrganizationMembershipsResult> MakeMembershipDefaultAsync(long userId, long membershipId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists an organization's tags (<c>GET /api/v2/organizations/{id}/tags.json</c>; requires organization
    ///     tagging to be enabled in Support). Set tags via <c>ZendeskOrganizationWrite.Tags</c> on an update.
    /// </summary>
    Task<ZendeskTagNamesResult> GetTagsAsync(long organizationId, CancellationToken cancellationToken = default);
}