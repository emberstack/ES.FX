using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.Groups;

/// <summary>
///     Default <see cref="IZendeskGroupsApi" /> implementation over the shared Zendesk <see cref="HttpClient" />.
/// </summary>
internal sealed class ZendeskGroupsApi(HttpClient httpClient, ILogger<ZendeskGroupsApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskGroupsApi
{
    /// <inheritdoc />
    public Task<ZendeskGroupsResult> ListAsync(int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("groups.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)),
            ("include", ZendeskQuery.Include(include)));
        return GetAsync<ZendeskGroupsResult>(requestUri, "Zendesk.Groups.List", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskGroup> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskGroupResponse>($"groups/{id}.json", "Zendesk.Groups.Get",
            cancellationToken).ConfigureAwait(false);
        return response.Group ?? throw new InvalidOperationException($"Zendesk group '{id}' was not found.");
    }

    /// <inheritdoc />
    public Task<ZendeskGroupMembershipsResult> GetMembershipsAsync(long groupId, int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"groups/{groupId}/memberships.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)),
            ("include", ZendeskQuery.Include(include)));
        return GetAsync<ZendeskGroupMembershipsResult>(requestUri, "Zendesk.Groups.Memberships", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskGroupsResult> GetAssignableAsync(int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("groups/assignable.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskGroupsResult>(requestUri, "Zendesk.Groups.Assignable", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskCount> CountAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskCountResponse>("groups/count.json", "Zendesk.Groups.Count",
            cancellationToken).ConfigureAwait(false);
        return response.Count ?? throw new InvalidOperationException("Zendesk returned no group count.");
    }

    /// <inheritdoc />
    public Task<ZendeskUsersResult> GetUsersAsync(long groupId, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"groups/{groupId}/users.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskUsersResult>(requestUri, "Zendesk.Groups.Users", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskGroup> CreateAsync(ZendeskGroupWrite group,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<ZendeskGroupResponse>("groups.json", new { group }, "Zendesk.Groups.Create",
            cancellationToken).ConfigureAwait(false);
        return response.Group ?? throw new InvalidOperationException("Zendesk returned no created group.");
    }

    /// <inheritdoc />
    public async Task<ZendeskGroup> UpdateAsync(long id, ZendeskGroupWrite group,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsync<ZendeskGroupResponse>($"groups/{id}.json", new { group },
            "Zendesk.Groups.Update", cancellationToken).ConfigureAwait(false);
        return response.Group ?? throw new InvalidOperationException($"Zendesk returned no group for '{id}'.");
    }

    /// <inheritdoc />
    public Task DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"groups/{id}.json", null, "Zendesk.Groups.Delete", cancellationToken);

    /// <inheritdoc />
    public async Task<ZendeskGroupMembership> CreateMembershipAsync(long userId, long groupId,
        bool? @default = null, CancellationToken cancellationToken = default)
    {
        var payload = new { group_membership = new { user_id = userId, group_id = groupId, @default } };
        var response = await PostAsync<ZendeskGroupMembershipResponse>("group_memberships.json", payload,
            "Zendesk.Groups.CreateMembership", cancellationToken).ConfigureAwait(false);
        return response.GroupMembership
               ?? throw new InvalidOperationException("Zendesk returned no created group membership.");
    }

    /// <inheritdoc />
    public Task<ZendeskJobStatus> CreateManyMembershipsAsync(IReadOnlyList<ZendeskGroupMembership> memberships,
        CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(memberships.Count, nameof(memberships));
        // Project to a clean payload so read-model defaults (e.g. Id = 0) never leak into the request.
        var payload = new
        {
            group_memberships = memberships
                .Select(m => new { user_id = m.UserId, group_id = m.GroupId, @default = m.Default })
                .ToList()
        };
        return SendJobAsync(HttpMethod.Post, "group_memberships/create_many.json", payload,
            "Zendesk.Groups.CreateManyMemberships", cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteMembershipAsync(long membershipId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"group_memberships/{membershipId}.json", null,
            "Zendesk.Groups.DeleteMembership", cancellationToken);

    /// <inheritdoc />
    public Task<ZendeskJobStatus> DeleteManyMembershipsAsync(IReadOnlyList<long> membershipIds,
        CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(membershipIds.Count, nameof(membershipIds));
        var requestUri = ZendeskQuery.Build("group_memberships/destroy_many.json",
            ("ids", string.Join(',', membershipIds)));
        return SendJobAsync(HttpMethod.Delete, requestUri, null, "Zendesk.Groups.DeleteManyMemberships",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskGroupMembershipsResult> MakeMembershipDefaultAsync(long userId, long membershipId,
        CancellationToken cancellationToken = default) =>
        PutAsync<ZendeskGroupMembershipsResult>(
            $"users/{userId}/group_memberships/{membershipId}/make_default.json",
            "Zendesk.Groups.MakeMembershipDefault", cancellationToken);
}