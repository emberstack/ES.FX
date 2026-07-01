using System.Text.Json;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A single SLA/metric event on a ticket (<c>GET /api/v2/tickets/{id}/metric_events</c>). Unlike the aggregate
///     <see cref="ZendeskTicketMetric" />, these are the timestamped lifecycle events for each metric
///     (<c>reply_time</c>, <c>resolution_time</c>, ...) including SLA <c>apply_sla</c>/<c>breach</c> events — the
///     actionable urgency signal.
/// </summary>
public sealed record ZendeskMetricEvent
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("ticket_id")] public long? TicketId { get; init; }

    /// <summary>The metric (e.g. <c>reply_time</c>, <c>resolution_time</c>, <c>agent_work_time</c>).</summary>
    [JsonPropertyName("metric")]
    public string? Metric { get; init; }

    /// <summary>
    ///     The event type (e.g. <c>create</c>, <c>activate</c>, <c>pause</c>, <c>fulfill</c>, <c>apply_sla</c>,
    ///     <c>breach</c>).
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("instance_id")] public long? InstanceId { get; init; }
    [JsonPropertyName("time")] public DateTimeOffset? Time { get; init; }
    [JsonPropertyName("deleted")] public bool? Deleted { get; init; }

    /// <summary>For a <c>breach</c>/<c>apply_sla</c> event, the SLA policy details (heterogeneous shape).</summary>
    [JsonPropertyName("sla")]
    public JsonElement? Sla { get; init; }

    /// <summary>For a <c>fulfill</c>/<c>update_status</c> event, the elapsed time in calendar/business minutes.</summary>
    [JsonPropertyName("status")]
    public JsonElement? Status { get; init; }
}