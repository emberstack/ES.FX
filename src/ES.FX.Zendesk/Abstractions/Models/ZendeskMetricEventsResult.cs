using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A page of the ticket metric events export
///     (<c>GET /api/v2/incremental/ticket_metric_events?start_time=...</c> — the only endpoint Zendesk provides for
///     metric events; there is no per-ticket variant). Pass <see cref="EndTime" /> as the next <c>start_time</c> to
///     continue; <see cref="EndOfStream" /> is <c>true</c> once the export is caught up.
/// </summary>
public sealed record ZendeskMetricEventsResult
{
    [JsonPropertyName("ticket_metric_events")]
    public IReadOnlyList<ZendeskMetricEvent> MetricEvents { get; init; } = [];

    [JsonPropertyName("count")] public int? Count { get; init; }

    /// <summary>The Unix-epoch timestamp to use as the next request's <c>start_time</c>.</summary>
    [JsonPropertyName("end_time")]
    public long? EndTime { get; init; }

    /// <summary><c>true</c> when the export has caught up with the present.</summary>
    [JsonPropertyName("end_of_stream")]
    public bool? EndOfStream { get; init; }

    [JsonPropertyName("next_page")] public string? NextPage { get; init; }
}