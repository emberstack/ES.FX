using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.Users;

/// <summary>
///     Default <see cref="IZendeskUsersApi" /> implementation over the shared Zendesk <see cref="HttpClient" />.
/// </summary>
internal sealed class ZendeskUsersApi(HttpClient httpClient, ILogger<ZendeskUsersApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskUsersApi
{
    /// <summary>Zendesk's documented maximum for <c>show_many</c>; larger lists are rejected with 400.</summary>
    private const int MaxIdsPerShowManyRequest = 100;

    /// <inheritdoc />
    public async Task<ZendeskUser> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskUserResponse>("users/me.json", "Zendesk.Users.GetCurrent",
            cancellationToken).ConfigureAwait(false);
        return response.User
               ?? throw new InvalidOperationException("Zendesk returned an empty response for the current user.");
    }

    /// <inheritdoc />
    public async Task<ZendeskUser> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskUserResponse>($"users/{id}.json", "Zendesk.Users.Get", cancellationToken)
            .ConfigureAwait(false);
        return response.User ?? throw new InvalidOperationException($"Zendesk user '{id}' was not found.");
    }

    /// <inheritdoc />
    public Task<ZendeskUsersResult> SearchAsync(string query, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("users/search.json",
            ("query", query), ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskUsersResult>(requestUri, "Zendesk.Users.Search", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskUsersResult> GetManyAsync(IReadOnlyList<long> ids,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return new ZendeskUsersResult();

        if (ids.Count <= MaxIdsPerShowManyRequest)
        {
            var requestUri = ZendeskQuery.Build("users/show_many.json",
                ("ids", string.Join(',', ids)), ("include", ZendeskQuery.Include(include)));
            return await GetAsync<ZendeskUsersResult>(requestUri, "Zendesk.Users.GetMany", cancellationToken)
                .ConfigureAwait(false);
        }

        // show_many rejects more than 100 ids with 400 Bad Request — chunk and merge instead of failing the
        // batch. Sideload arrays are merged across chunks and de-duplicated by id.
        var users = new List<ZendeskUser>(ids.Count);
        List<ZendeskOrganization>? organizations = null;
        List<ZendeskGroup>? groups = null;
        List<ZendeskUserIdentity>? identities = null;
        for (var offset = 0; offset < ids.Count; offset += MaxIdsPerShowManyRequest)
        {
            var chunk = ids.Skip(offset).Take(MaxIdsPerShowManyRequest);
            var requestUri = ZendeskQuery.Build("users/show_many.json",
                ("ids", string.Join(',', chunk)), ("include", ZendeskQuery.Include(include)));
            var page = await GetAsync<ZendeskUsersResult>(requestUri, "Zendesk.Users.GetMany", cancellationToken)
                .ConfigureAwait(false);
            users.AddRange(page.Users);
            if (page.Organizations is not null) (organizations ??= []).AddRange(page.Organizations);
            if (page.Groups is not null) (groups ??= []).AddRange(page.Groups);
            if (page.Identities is not null) (identities ??= []).AddRange(page.Identities);
        }

        return new ZendeskUsersResult
        {
            Users = users,
            Count = users.Count,
            Organizations = organizations?.DistinctBy(o => o.Id).ToList(),
            Groups = groups?.DistinctBy(g => g.Id).ToList(),
            Identities = identities?.DistinctBy(i => i.Id).ToList()
        };
    }

    /// <inheritdoc />
    public Task<ZendeskTicketsResult> GetRequestedTicketsAsync(long userId, int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"users/{userId}/tickets/requested.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)),
            ("include", ZendeskQuery.Include(include)));
        return GetAsync<ZendeskTicketsResult>(requestUri, "Zendesk.Users.RequestedTickets", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskUsersResult> ListAsync(string? role = null, int? pageSize = null, string? afterCursor = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("users.json",
            ("role", role), ("page[size]", ZendeskQuery.Int(pageSize)), ("page[after]", afterCursor),
            ("include", ZendeskQuery.Include(include)));
        return GetAsync<ZendeskUsersResult>(requestUri, "Zendesk.Users.List", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskCount> CountAsync(string? role = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("users/count.json", ("role", role));
        var response = await GetAsync<ZendeskCountResponse>(requestUri, "Zendesk.Users.Count", cancellationToken)
            .ConfigureAwait(false);
        return response.Count ?? throw new InvalidOperationException("Zendesk returned no user count.");
    }

    /// <inheritdoc />
    public Task<ZendeskUsersResult> AutocompleteAsync(string name, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var requestUri = ZendeskQuery.Build("users/autocomplete.json",
            ("name", name), ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskUsersResult>(requestUri, "Zendesk.Users.Autocomplete", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskUserRelated> GetRelatedInformationAsync(long userId,
        CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskUserRelatedResponse>($"users/{userId}/related.json",
            "Zendesk.Users.Related", cancellationToken).ConfigureAwait(false);
        return response.UserRelated
               ?? throw new InvalidOperationException(
                   $"Zendesk returned no related information for user '{userId}'.");
    }

    /// <inheritdoc />
    public Task<ZendeskUserIdentitiesResult> GetIdentitiesAsync(long userId, int? pageSize = null,
        string? afterCursor = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"users/{userId}/identities.json",
            ("page[size]", ZendeskQuery.Int(pageSize)), ("page[after]", afterCursor));
        return GetAsync<ZendeskUserIdentitiesResult>(requestUri, "Zendesk.Users.Identities", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskGroupsResult> GetGroupsAsync(long userId, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"users/{userId}/groups.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskGroupsResult>(requestUri, "Zendesk.Users.Groups", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskOrganizationsResult> GetOrganizationsAsync(long userId, int? page = null,
        int? perPage = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"users/{userId}/organizations.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskOrganizationsResult>(requestUri, "Zendesk.Users.Organizations", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskTicketsResult> GetAssignedTicketsAsync(long userId, int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"users/{userId}/tickets/assigned.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)),
            ("include", ZendeskQuery.Include(include)));
        return GetAsync<ZendeskTicketsResult>(requestUri, "Zendesk.Users.AssignedTickets", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskTicketsResult> GetCcdTicketsAsync(long userId, int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"users/{userId}/tickets/ccd.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)),
            ("include", ZendeskQuery.Include(include)));
        return GetAsync<ZendeskTicketsResult>(requestUri, "Zendesk.Users.CcdTickets", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskUser> CreateAsync(ZendeskUserWrite user, CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<ZendeskUserResponse>("users.json", new { user }, "Zendesk.Users.Create",
            cancellationToken).ConfigureAwait(false);
        return response.User ?? throw new InvalidOperationException("Zendesk returned no created user.");
    }

    /// <inheritdoc />
    public async Task<ZendeskUser> CreateOrUpdateAsync(ZendeskUserWrite user,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<ZendeskUserResponse>("users/create_or_update.json", new { user },
            "Zendesk.Users.CreateOrUpdate", cancellationToken).ConfigureAwait(false);
        return response.User ?? throw new InvalidOperationException("Zendesk returned no user.");
    }

    /// <inheritdoc />
    public Task<ZendeskJobStatus> CreateManyAsync(IReadOnlyList<ZendeskUserWrite> users,
        CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(users.Count, nameof(users));
        return SendJobAsync(HttpMethod.Post, "users/create_many.json", new { users }, "Zendesk.Users.CreateMany",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskJobStatus> CreateOrUpdateManyAsync(IReadOnlyList<ZendeskUserWrite> users,
        CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(users.Count, nameof(users));
        return SendJobAsync(HttpMethod.Post, "users/create_or_update_many.json", new { users },
            "Zendesk.Users.CreateOrUpdateMany", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskUser> UpdateAsync(long id, ZendeskUserWrite user,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsync<ZendeskUserResponse>($"users/{id}.json", new { user }, "Zendesk.Users.Update",
            cancellationToken).ConfigureAwait(false);
        return response.User ?? throw new InvalidOperationException($"Zendesk returned no user for '{id}'.");
    }

    /// <inheritdoc />
    public Task<ZendeskJobStatus> UpdateManyAsync(IReadOnlyList<long> ids, ZendeskUserWrite change,
        CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(ids.Count, nameof(ids));
        var requestUri = ZendeskQuery.Build("users/update_many.json", ("ids", string.Join(',', ids)));
        return SendJobAsync(HttpMethod.Put, requestUri, new { user = change }, "Zendesk.Users.UpdateMany",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskJobStatus> UpdateManyAsync(IReadOnlyList<ZendeskUserWrite> users,
        CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(users.Count, nameof(users));
        if (users.Any(u => u.Id is null))
            throw new ArgumentException("Every batch update item must carry Id.", nameof(users));
        return SendJobAsync(HttpMethod.Put, "users/update_many.json", new { users },
            "Zendesk.Users.UpdateManyBatch", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskUser> MergeAsync(long loserUserId, long winnerUserId,
        CancellationToken cancellationToken = default)
    {
        // DIRECTION: the path user is absorbed INTO the body user; the winner is returned.
        var response = await PutAsync<ZendeskUserResponse>($"users/{loserUserId}/merge.json",
                new { user = new { id = winnerUserId } }, "Zendesk.Users.Merge", cancellationToken)
            .ConfigureAwait(false);
        return response.User ?? throw new InvalidOperationException("Zendesk returned no merged user.");
    }

    /// <inheritdoc />
    public async Task<ZendeskUser> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        // Zendesk answers 200 with the soft-deleted user (active=false) — not 204.
        var response = await DeleteAsync<ZendeskUserResponse>($"users/{id}.json", "Zendesk.Users.Delete",
            cancellationToken).ConfigureAwait(false);
        return response.User ?? throw new InvalidOperationException($"Zendesk returned no user for '{id}'.");
    }

    /// <inheritdoc />
    public Task<ZendeskJobStatus> DeleteManyAsync(IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(ids.Count, nameof(ids));
        var requestUri = ZendeskQuery.Build("users/destroy_many.json", ("ids", string.Join(',', ids)));
        return SendJobAsync(HttpMethod.Delete, requestUri, null, "Zendesk.Users.DeleteMany", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskUser> DeletePermanentlyAsync(long deletedUserId,
        CancellationToken cancellationToken = default)
    {
        var response = await DeleteAsync<ZendeskDeletedUserResponse>($"deleted_users/{deletedUserId}.json",
            "Zendesk.Users.DeletePermanently", cancellationToken).ConfigureAwait(false);
        return response.DeletedUser
               ?? throw new InvalidOperationException($"Zendesk returned no deleted user for '{deletedUserId}'.");
    }

    /// <inheritdoc />
    public async Task<ZendeskUserIdentity> CreateIdentityAsync(long userId, ZendeskUserIdentityWrite identity,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<ZendeskUserIdentityResponse>($"users/{userId}/identities.json",
            new { identity }, "Zendesk.Users.CreateIdentity", cancellationToken).ConfigureAwait(false);
        return response.Identity ?? throw new InvalidOperationException("Zendesk returned no created identity.");
    }

    /// <inheritdoc />
    public async Task<ZendeskUserIdentity> UpdateIdentityAsync(long userId, long identityId,
        ZendeskUserIdentityWrite identity, CancellationToken cancellationToken = default)
    {
        var response = await PutAsync<ZendeskUserIdentityResponse>(
            $"users/{userId}/identities/{identityId}.json", new { identity }, "Zendesk.Users.UpdateIdentity",
            cancellationToken).ConfigureAwait(false);
        return response.Identity
               ?? throw new InvalidOperationException($"Zendesk returned no identity for '{identityId}'.");
    }

    /// <inheritdoc />
    public Task<ZendeskUserIdentitiesResult> MakeIdentityPrimaryAsync(long userId, long identityId,
        CancellationToken cancellationToken = default) =>
        // Collection-level operation: the response is the user's FULL identity list.
        PutAsync<ZendeskUserIdentitiesResult>($"users/{userId}/identities/{identityId}/make_primary.json",
            "Zendesk.Users.MakeIdentityPrimary", cancellationToken);

    /// <inheritdoc />
    public async Task<ZendeskUserIdentity> VerifyIdentityAsync(long userId, long identityId,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsync<ZendeskUserIdentityResponse>(
            $"users/{userId}/identities/{identityId}/verify.json", "Zendesk.Users.VerifyIdentity",
            cancellationToken).ConfigureAwait(false);
        return response.Identity
               ?? throw new InvalidOperationException($"Zendesk returned no identity for '{identityId}'.");
    }

    /// <inheritdoc />
    public Task RequestIdentityVerificationAsync(long userId, long identityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"users/{userId}/identities/{identityId}/request_verification.json", null,
            "Zendesk.Users.RequestIdentityVerification", cancellationToken);

    /// <inheritdoc />
    public Task DeleteIdentityAsync(long userId, long identityId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"users/{userId}/identities/{identityId}.json", null,
            "Zendesk.Users.DeleteIdentity", cancellationToken);

    /// <inheritdoc />
    public Task<ZendeskTagNamesResult> GetTagsAsync(long userId, CancellationToken cancellationToken = default) =>
        GetAsync<ZendeskTagNamesResult>($"users/{userId}/tags.json", "Zendesk.Users.Tags", cancellationToken);
}