using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk <c>job_statuses</c> resource — the async jobs returned by bulk operations.
///     Zendesk retains job status data for roughly one day.
/// </summary>
public interface IZendeskJobStatusesApi
{
    /// <summary>Lists recent job statuses (<c>GET /api/v2/job_statuses.json</c>; cursor-paginated only).</summary>
    Task<ZendeskJobStatusesResult> ListAsync(int? pageSize = null, string? afterCursor = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a job status by id (<c>GET /api/v2/job_statuses/{id}.json</c>).</summary>
    Task<ZendeskJobStatus> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns many job statuses in one request
    ///     (<c>GET /api/v2/job_statuses/show_many.json?ids=</c>; up to 100 ids).
    /// </summary>
    Task<ZendeskJobStatusesResult> GetManyAsync(IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default);
}