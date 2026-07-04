using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.CustomStatuses;

/// <summary>
///     Default <see cref="IZendeskCustomStatusesApi" /> implementation over the shared Zendesk
///     <see cref="HttpClient" />.
/// </summary>
internal sealed class ZendeskCustomStatusesApi(HttpClient httpClient, ILogger<ZendeskCustomStatusesApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskCustomStatusesApi
{
    /// <inheritdoc />
    public Task<ZendeskCustomStatusesResult> ListAsync(bool? active = null, bool? @default = null,
        string? statusCategories = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("custom_statuses.json",
            ("active", ZendeskQuery.Bool(active)), ("default", ZendeskQuery.Bool(@default)),
            ("status_categories", statusCategories));
        return GetAsync<ZendeskCustomStatusesResult>(requestUri, "Zendesk.CustomStatuses.List", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskCustomStatus> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskCustomStatusResponse>($"custom_statuses/{id}.json",
            "Zendesk.CustomStatuses.Get", cancellationToken).ConfigureAwait(false);
        return response.CustomStatus
               ?? throw new InvalidOperationException($"Zendesk custom status '{id}' was not found.");
    }

    /// <inheritdoc />
    public async Task<ZendeskCustomStatus> CreateAsync(ZendeskCustomStatusWrite status,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<ZendeskCustomStatusResponse>("custom_statuses.json",
                new { custom_status = status }, "Zendesk.CustomStatuses.Create", cancellationToken)
            .ConfigureAwait(false);
        return response.CustomStatus
               ?? throw new InvalidOperationException("Zendesk returned no created custom status.");
    }

    /// <inheritdoc />
    public async Task<ZendeskCustomStatus> UpdateAsync(long id, ZendeskCustomStatusWrite status,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsync<ZendeskCustomStatusResponse>($"custom_statuses/{id}.json",
                new { custom_status = status }, "Zendesk.CustomStatuses.Update", cancellationToken)
            .ConfigureAwait(false);
        return response.CustomStatus
               ?? throw new InvalidOperationException($"Zendesk returned no custom status for '{id}'.");
    }

    /// <inheritdoc />
    public Task DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"custom_statuses/{id}.json", null, "Zendesk.CustomStatuses.Delete",
            cancellationToken);
}