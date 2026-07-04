using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.JobStatuses;

/// <summary>
///     Default <see cref="IZendeskJobStatusesApi" /> implementation over the shared Zendesk
///     <see cref="HttpClient" />.
/// </summary>
internal sealed class ZendeskJobStatusesApi(HttpClient httpClient, ILogger<ZendeskJobStatusesApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskJobStatusesApi
{
    /// <summary>Zendesk's documented maximum for <c>show_many</c> id lists.</summary>
    private const int MaxIdsPerShowManyRequest = 100;

    /// <inheritdoc />
    public Task<ZendeskJobStatusesResult> ListAsync(int? pageSize = null, string? afterCursor = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("job_statuses.json",
            ("page[size]", ZendeskQuery.Int(pageSize)), ("page[after]", afterCursor));
        return GetAsync<ZendeskJobStatusesResult>(requestUri, "Zendesk.JobStatuses.List", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskJobStatus> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var response = await GetAsync<ZendeskJobStatusResponse>($"job_statuses/{Uri.EscapeDataString(id)}.json",
            "Zendesk.JobStatuses.Get", cancellationToken).ConfigureAwait(false);
        return response.JobStatus
               ?? throw new InvalidOperationException($"Zendesk job status '{id}' was not found.");
    }

    /// <inheritdoc />
    public Task<ZendeskJobStatusesResult> GetManyAsync(IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return Task.FromResult(new ZendeskJobStatusesResult());
        if (ids.Count > MaxIdsPerShowManyRequest)
            throw new ArgumentException(
                $"Zendesk accepts at most {MaxIdsPerShowManyRequest} job status ids per request.", nameof(ids));

        var requestUri = ZendeskQuery.Build("job_statuses/show_many.json", ("ids", string.Join(',', ids)));
        return GetAsync<ZendeskJobStatusesResult>(requestUri, "Zendesk.JobStatuses.GetMany", cancellationToken);
    }
}