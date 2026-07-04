using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.Organizations;

/// <summary>
///     Default <see cref="IZendeskOrganizationsApi" /> implementation over the shared Zendesk <see cref="HttpClient" />.
/// </summary>
internal sealed class ZendeskOrganizationsApi(HttpClient httpClient, ILogger<ZendeskOrganizationsApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskOrganizationsApi
{
    /// <summary>Zendesk's documented maximum for <c>show_many</c>; larger lists are rejected with 400.</summary>
    private const int MaxIdsPerShowManyRequest = 100;

    /// <inheritdoc />
    public async Task<ZendeskOrganization> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskOrganizationResponse>($"organizations/{id}.json",
            "Zendesk.Organizations.Get", cancellationToken).ConfigureAwait(false);
        return response.Organization
               ?? throw new InvalidOperationException($"Zendesk organization '{id}' was not found.");
    }

    /// <inheritdoc />
    public Task<ZendeskTicketsResult> GetTicketsAsync(long organizationId, int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"organizations/{organizationId}/tickets.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)),
            ("include", ZendeskQuery.Include(include)));
        return GetAsync<ZendeskTicketsResult>(requestUri, "Zendesk.Organizations.Tickets", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskOrganizationsResult> ListAsync(int? pageSize = null, string? afterCursor = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("organizations.json",
            ("page[size]", ZendeskQuery.Int(pageSize)), ("page[after]", afterCursor));
        return GetAsync<ZendeskOrganizationsResult>(requestUri, "Zendesk.Organizations.List", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskCount> CountAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskCountResponse>("organizations/count.json",
            "Zendesk.Organizations.Count", cancellationToken).ConfigureAwait(false);
        return response.Count ?? throw new InvalidOperationException("Zendesk returned no organization count.");
    }

    /// <inheritdoc />
    public async Task<ZendeskOrganizationsResult> GetManyAsync(IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return new ZendeskOrganizationsResult();

        if (ids.Count <= MaxIdsPerShowManyRequest)
        {
            var requestUri = ZendeskQuery.Build("organizations/show_many.json", ("ids", string.Join(',', ids)));
            return await GetAsync<ZendeskOrganizationsResult>(requestUri, "Zendesk.Organizations.GetMany",
                cancellationToken).ConfigureAwait(false);
        }

        // show_many rejects more than 100 ids with 400 Bad Request — chunk and merge instead of failing the batch.
        var organizations = new List<ZendeskOrganization>(ids.Count);
        for (var offset = 0; offset < ids.Count; offset += MaxIdsPerShowManyRequest)
        {
            var chunk = ids.Skip(offset).Take(MaxIdsPerShowManyRequest);
            var requestUri = ZendeskQuery.Build("organizations/show_many.json", ("ids", string.Join(',', chunk)));
            var page = await GetAsync<ZendeskOrganizationsResult>(requestUri, "Zendesk.Organizations.GetMany",
                cancellationToken).ConfigureAwait(false);
            organizations.AddRange(page.Organizations);
        }

        return new ZendeskOrganizationsResult { Organizations = organizations, Count = organizations.Count };
    }

    /// <inheritdoc />
    public Task<ZendeskOrganizationsResult> SearchAsync(string? name = null, string? externalId = null,
        CancellationToken cancellationToken = default)
    {
        // The endpoint is an exact-match lookup on ONE of the two attributes — Zendesk rejects both/neither.
        if (string.IsNullOrWhiteSpace(name) == string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("Provide exactly one of name or externalId.", nameof(name));

        var requestUri = ZendeskQuery.Build("organizations/search.json",
            ("name", name), ("external_id", externalId));
        return GetAsync<ZendeskOrganizationsResult>(requestUri, "Zendesk.Organizations.Search", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskOrganizationsResult> AutocompleteAsync(string name, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var requestUri = ZendeskQuery.Build("organizations/autocomplete.json",
            ("name", name), ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskOrganizationsResult>(requestUri, "Zendesk.Organizations.Autocomplete",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskUsersResult> GetUsersAsync(long organizationId, int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"organizations/{organizationId}/users.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)),
            ("include", ZendeskQuery.Include(include)));
        return GetAsync<ZendeskUsersResult>(requestUri, "Zendesk.Organizations.Users", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskOrganizationMembershipsResult> GetMembershipsAsync(long organizationId, int? page = null,
        int? perPage = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"organizations/{organizationId}/organization_memberships.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskOrganizationMembershipsResult>(requestUri, "Zendesk.Organizations.Memberships",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskOrganization> CreateAsync(ZendeskOrganizationWrite organization,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<ZendeskOrganizationResponse>("organizations.json", new { organization },
            "Zendesk.Organizations.Create", cancellationToken).ConfigureAwait(false);
        return response.Organization
               ?? throw new InvalidOperationException("Zendesk returned no created organization.");
    }

    /// <inheritdoc />
    public Task<ZendeskJobStatus> CreateManyAsync(IReadOnlyList<ZendeskOrganizationWrite> organizations,
        CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(organizations.Count, nameof(organizations));
        return SendJobAsync(HttpMethod.Post, "organizations/create_many.json", new { organizations },
            "Zendesk.Organizations.CreateMany", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskOrganization> CreateOrUpdateAsync(ZendeskOrganizationWrite organization,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<ZendeskOrganizationResponse>("organizations/create_or_update.json",
            new { organization }, "Zendesk.Organizations.CreateOrUpdate", cancellationToken).ConfigureAwait(false);
        return response.Organization ?? throw new InvalidOperationException("Zendesk returned no organization.");
    }

    /// <inheritdoc />
    public async Task<ZendeskOrganization> UpdateAsync(long id, ZendeskOrganizationWrite organization,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsync<ZendeskOrganizationResponse>($"organizations/{id}.json",
            new { organization }, "Zendesk.Organizations.Update", cancellationToken).ConfigureAwait(false);
        return response.Organization
               ?? throw new InvalidOperationException($"Zendesk returned no organization for '{id}'.");
    }

    /// <inheritdoc />
    public Task<ZendeskJobStatus> UpdateManyAsync(IReadOnlyList<long> ids, ZendeskOrganizationWrite change,
        CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(ids.Count, nameof(ids));
        var requestUri = ZendeskQuery.Build("organizations/update_many.json", ("ids", string.Join(',', ids)));
        return SendJobAsync(HttpMethod.Put, requestUri, new { organization = change },
            "Zendesk.Organizations.UpdateMany", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskJobStatus> UpdateManyAsync(IReadOnlyList<ZendeskOrganizationWrite> organizations,
        CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(organizations.Count, nameof(organizations));
        if (organizations.Any(o => o.Id is null))
            throw new ArgumentException("Every batch update item must carry Id.", nameof(organizations));
        return SendJobAsync(HttpMethod.Put, "organizations/update_many.json", new { organizations },
            "Zendesk.Organizations.UpdateManyBatch", cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"organizations/{id}.json", null, "Zendesk.Organizations.Delete",
            cancellationToken);

    /// <inheritdoc />
    public Task<ZendeskJobStatus> DeleteManyAsync(IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(ids.Count, nameof(ids));
        var requestUri = ZendeskQuery.Build("organizations/destroy_many.json", ("ids", string.Join(',', ids)));
        return SendJobAsync(HttpMethod.Delete, requestUri, null, "Zendesk.Organizations.DeleteMany",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskOrganizationMerge> MergeAsync(long loserOrganizationId, long winnerOrganizationId,
        CancellationToken cancellationToken = default)
    {
        // Async but NOT a job_status — poll GetMergeAsync with the returned (string) id.
        var response = await PostAsync<ZendeskOrganizationMergeResponse>(
            $"organizations/{loserOrganizationId}/merge.json",
            new { organization_merge = new { winner_id = winnerOrganizationId } }, "Zendesk.Organizations.Merge",
            cancellationToken).ConfigureAwait(false);
        return response.OrganizationMerge
               ?? throw new InvalidOperationException("Zendesk returned no organization merge.");
    }

    /// <inheritdoc />
    public async Task<ZendeskOrganizationMerge> GetMergeAsync(string mergeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mergeId);
        var response = await GetAsync<ZendeskOrganizationMergeResponse>(
            $"organization_merges/{Uri.EscapeDataString(mergeId)}.json", "Zendesk.Organizations.GetMerge",
            cancellationToken).ConfigureAwait(false);
        return response.OrganizationMerge
               ?? throw new InvalidOperationException($"Zendesk organization merge '{mergeId}' was not found.");
    }

    /// <inheritdoc />
    public async Task<ZendeskOrganizationMembership> CreateMembershipAsync(long userId, long organizationId,
        bool? @default = null, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            organization_membership = new { user_id = userId, organization_id = organizationId, @default }
        };
        var response = await PostAsync<ZendeskOrganizationMembershipResponse>("organization_memberships.json",
            payload, "Zendesk.Organizations.CreateMembership", cancellationToken).ConfigureAwait(false);
        return response.OrganizationMembership
               ?? throw new InvalidOperationException("Zendesk returned no created membership.");
    }

    /// <inheritdoc />
    public Task<ZendeskJobStatus> CreateManyMembershipsAsync(
        IReadOnlyList<ZendeskOrganizationMembership> memberships, CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(memberships.Count, nameof(memberships));
        // Project to a clean payload so read-model defaults (e.g. Id = 0) never leak into the request.
        var payload = new
        {
            organization_memberships = memberships
                .Select(m => new { user_id = m.UserId, organization_id = m.OrganizationId, @default = m.Default })
                .ToList()
        };
        return SendJobAsync(HttpMethod.Post, "organization_memberships/create_many.json", payload,
            "Zendesk.Organizations.CreateManyMemberships", cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteMembershipAsync(long membershipId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"organization_memberships/{membershipId}.json", null,
            "Zendesk.Organizations.DeleteMembership", cancellationToken);

    /// <inheritdoc />
    public Task<ZendeskJobStatus> DeleteManyMembershipsAsync(IReadOnlyList<long> membershipIds,
        CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(membershipIds.Count, nameof(membershipIds));
        var requestUri = ZendeskQuery.Build("organization_memberships/destroy_many.json",
            ("ids", string.Join(',', membershipIds)));
        return SendJobAsync(HttpMethod.Delete, requestUri, null, "Zendesk.Organizations.DeleteManyMemberships",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskOrganizationMembershipsResult> MakeMembershipDefaultAsync(long userId, long membershipId,
        CancellationToken cancellationToken = default) =>
        PutAsync<ZendeskOrganizationMembershipsResult>(
            $"users/{userId}/organization_memberships/{membershipId}/make_default.json",
            "Zendesk.Organizations.MakeMembershipDefault", cancellationToken);

    /// <inheritdoc />
    public Task<ZendeskTagNamesResult> GetTagsAsync(long organizationId,
        CancellationToken cancellationToken = default) =>
        GetAsync<ZendeskTagNamesResult>($"organizations/{organizationId}/tags.json", "Zendesk.Organizations.Tags",
            cancellationToken);
}