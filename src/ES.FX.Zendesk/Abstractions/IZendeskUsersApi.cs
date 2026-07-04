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
    ///     <paramref name="include" /> sideloads (<c>organizations</c>, <c>groups</c>, <c>identities</c>)
    ///     resolve related records in the same roundtrip (merged and de-duplicated across chunks).
    /// </summary>
    Task<ZendeskUsersResult> GetManyAsync(IReadOnlyList<long> ids, IReadOnlyList<string>? include = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the tickets requested by a user — their ticket history
    ///     (<c>GET /api/v2/users/{id}/tickets/requested.json</c>). <paramref name="include" /> sideloads
    ///     (<c>users</c>, <c>groups</c>, <c>organizations</c>) resolve related records inline as sibling arrays.
    /// </summary>
    Task<ZendeskTicketsResult> GetRequestedTicketsAsync(long userId, int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists users (<c>GET /api/v2/users.json</c>; cursor-paginated — page with <c>Meta.AfterCursor</c>).
    /// </summary>
    /// <param name="role">Optional role filter — see <see cref="ZendeskUserRoles" />.</param>
    /// <param name="pageSize">The cursor page size (max 100).</param>
    /// <param name="afterCursor">The cursor from the previous page's <c>Meta.AfterCursor</c>.</param>
    /// <param name="include">
    ///     Sideloads (<c>organizations</c>, <c>groups</c>, <c>identities</c>) resolved inline as sibling arrays.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<ZendeskUsersResult> ListAsync(string? role = null, int? pageSize = null, string? afterCursor = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the (cached, approximate) user count (<c>GET /api/v2/users/count.json</c>), optionally
    ///     filtered by role.
    /// </summary>
    Task<ZendeskCount> CountAsync(string? role = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Suggests users whose name or e-mail starts with a prefix
    ///     (<c>GET /api/v2/users/autocomplete.json?name=</c>; minimum two characters, offset-paginated only).
    /// </summary>
    Task<ZendeskUsersResult> AutocompleteAsync(string name, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a user's related ticket/subscription counts (<c>GET /api/v2/users/{id}/related.json</c>).
    /// </summary>
    Task<ZendeskUserRelated> GetRelatedInformationAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists a user's identities — e-mails, phone numbers, social handles
    ///     (<c>GET /api/v2/users/{id}/identities.json</c>).
    /// </summary>
    Task<ZendeskUserIdentitiesResult> GetIdentitiesAsync(long userId, int? pageSize = null,
        string? afterCursor = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists the groups an agent belongs to (<c>GET /api/v2/users/{id}/groups.json</c>).
    /// </summary>
    Task<ZendeskGroupsResult> GetGroupsAsync(long userId, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists the organizations a user belongs to (<c>GET /api/v2/users/{id}/organizations.json</c>).
    /// </summary>
    Task<ZendeskOrganizationsResult> GetOrganizationsAsync(long userId, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the tickets assigned to an agent (<c>GET /api/v2/users/{id}/tickets/assigned.json</c>).
    /// </summary>
    Task<ZendeskTicketsResult> GetAssignedTicketsAsync(long userId, int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the tickets a user is CC'd on (<c>GET /api/v2/users/{id}/tickets/ccd.json</c>).
    /// </summary>
    Task<ZendeskTicketsResult> GetCcdTicketsAsync(long userId, int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default);

    /// <summary>Creates a user (<c>POST /api/v2/users.json</c>). Duplicate e-mails fail with <c>422</c>.</summary>
    Task<ZendeskUser> CreateAsync(ZendeskUserWrite user, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates or updates a user matched by e-mail or external id
    ///     (<c>POST /api/v2/users/create_or_update.json</c>).
    /// </summary>
    Task<ZendeskUser> CreateOrUpdateAsync(ZendeskUserWrite user, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates up to 100 users as an async job (<c>POST /api/v2/users/create_many.json</c>). NOTE: bulk user
    ///     imports are off by default — Zendesk support must enable them or the call returns <c>403</c>.
    /// </summary>
    Task<ZendeskJobStatus> CreateManyAsync(IReadOnlyList<ZendeskUserWrite> users,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates or updates up to 100 users as an async job
    ///     (<c>POST /api/v2/users/create_or_update_many.json</c>). Same gating as <see cref="CreateManyAsync" />.
    /// </summary>
    Task<ZendeskJobStatus> CreateOrUpdateManyAsync(IReadOnlyList<ZendeskUserWrite> users,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates a user (<c>PUT /api/v2/users/{id}.json</c>). QUIRK: setting <c>Email</c> here adds a SECONDARY
    ///     identity — use the identity operations to change the primary e-mail.
    /// </summary>
    Task<ZendeskUser> UpdateAsync(long id, ZendeskUserWrite user, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Applies the SAME change to up to 100 users as an async job
    ///     (<c>PUT /api/v2/users/update_many.json?ids=</c>).
    /// </summary>
    Task<ZendeskJobStatus> UpdateManyAsync(IReadOnlyList<long> ids, ZendeskUserWrite change,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Applies PER-USER changes to up to 100 users as an async job (batch form). Every item must carry
    ///     <c>Id</c>.
    /// </summary>
    Task<ZendeskJobStatus> UpdateManyAsync(IReadOnlyList<ZendeskUserWrite> users,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Merges an end user INTO another (<c>PUT /api/v2/users/{loserId}/merge.json</c>). The path user is
    ///     absorbed; the winner survives and is returned. End users only.
    /// </summary>
    Task<ZendeskUser> MergeAsync(long loserUserId, long winnerUserId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Soft-deletes a user and returns the deleted record (<c>DELETE /api/v2/users/{id}.json</c>; documented
    ///     as NOT recoverable). GDPR purge additionally requires <see cref="DeletePermanentlyAsync" />.
    /// </summary>
    Task<ZendeskUser> DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes up to 100 users as an async job (<c>DELETE /api/v2/users/destroy_many.json</c>; admin-only).</summary>
    Task<ZendeskJobStatus> DeleteManyAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default);

    /// <summary>
    ///     PERMANENTLY deletes an already soft-deleted user (<c>DELETE /api/v2/deleted_users/{id}.json</c>).
    ///     Irreversible; dedicated rate limit of 700 per 10 minutes.
    /// </summary>
    Task<ZendeskUser> DeletePermanentlyAsync(long deletedUserId, CancellationToken cancellationToken = default);

    /// <summary>Adds an identity to a user (<c>POST /api/v2/users/{id}/identities.json</c>).</summary>
    Task<ZendeskUserIdentity> CreateIdentityAsync(long userId, ZendeskUserIdentityWrite identity,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates an identity's value/verification (<c>PUT /api/v2/users/{id}/identities/{identityId}.json</c>).
    ///     Cannot change <c>primary</c> — use <see cref="MakeIdentityPrimaryAsync" />.
    /// </summary>
    Task<ZendeskUserIdentity> UpdateIdentityAsync(long userId, long identityId, ZendeskUserIdentityWrite identity,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Makes an identity the user's primary (<c>PUT .../identities/{id}/make_primary</c>). Returns the user's
    ///     FULL identity list (the operation is collection-level).
    /// </summary>
    Task<ZendeskUserIdentitiesResult> MakeIdentityPrimaryAsync(long userId, long identityId,
        CancellationToken cancellationToken = default);

    /// <summary>Marks an identity verified (<c>PUT .../identities/{id}/verify</c>).</summary>
    Task<ZendeskUserIdentity> VerifyIdentityAsync(long userId, long identityId,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a verification e-mail for an identity (<c>PUT .../identities/{id}/request_verification</c>).</summary>
    Task RequestIdentityVerificationAsync(long userId, long identityId,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes an identity (<c>DELETE /api/v2/users/{id}/identities/{identityId}.json</c>).</summary>
    Task DeleteIdentityAsync(long userId, long identityId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists a user's tags (<c>GET /api/v2/users/{id}/tags.json</c>; requires user tagging to be enabled in
    ///     Support). Set tags via <c>ZendeskUserWrite.Tags</c> on an update.
    /// </summary>
    Task<ZendeskTagNamesResult> GetTagsAsync(long userId, CancellationToken cancellationToken = default);
}