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
        CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("groups.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
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
        CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"groups/{groupId}/memberships.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskGroupMembershipsResult>(requestUri, "Zendesk.Groups.Memberships", cancellationToken);
    }
}