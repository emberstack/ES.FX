using System.Text.Json;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     The status of an async Zendesk job (returned by bulk operations). Job status data is retained by Zendesk
///     for roughly one day after the job completes.
/// </summary>
public sealed record ZendeskJobStatus
{
    /// <summary>The job id (an opaque string, not numeric).</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The job state — see <see cref="ZendeskJobStatusValues" />.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("total")] public long? Total { get; init; }
    [JsonPropertyName("progress")] public long? Progress { get; init; }
    [JsonPropertyName("message")] public string? Message { get; init; }

    /// <summary>Per-item results; the shape varies by job type, so the raw JSON is exposed.</summary>
    [JsonPropertyName("results")]
    public JsonElement? Results { get; init; }
}

/// <summary>The <c>{ "job_status": {...} }</c> envelope.</summary>
public sealed record ZendeskJobStatusResponse
{
    [JsonPropertyName("job_status")] public ZendeskJobStatus? JobStatus { get; init; }
}

/// <summary>A page of job statuses (<c>{ "job_statuses": [...] }</c> envelope). Cursor-paginated.</summary>
public sealed record ZendeskJobStatusesResult
{
    [JsonPropertyName("job_statuses")] public IReadOnlyList<ZendeskJobStatus> JobStatuses { get; init; } = [];
    [JsonPropertyName("meta")] public ZendeskCursorMeta? Meta { get; init; }
}