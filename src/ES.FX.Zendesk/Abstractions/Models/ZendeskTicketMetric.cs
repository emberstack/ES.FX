using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     Timing and lifecycle metrics for a ticket (<c>GET /api/v2/tickets/{id}/metrics.json</c>): reply/resolution
///     times, reopens, replies and wait times.
/// </summary>
public sealed record ZendeskTicketMetric
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("ticket_id")] public long? TicketId { get; init; }

    /// <summary>Number of times the ticket was reopened (a recurring/unresolved-pain signal).</summary>
    [JsonPropertyName("reopens")]
    public int? Reopens { get; init; }

    /// <summary>Total number of agent replies.</summary>
    [JsonPropertyName("replies")]
    public int? Replies { get; init; }

    [JsonPropertyName("assignee_stations")]
    public int? AssigneeStations { get; init; }

    [JsonPropertyName("group_stations")] public int? GroupStations { get; init; }

    [JsonPropertyName("reply_time_in_minutes")]
    public ZendeskMinutes? ReplyTimeInMinutes { get; init; }

    [JsonPropertyName("first_resolution_time_in_minutes")]
    public ZendeskMinutes? FirstResolutionTimeInMinutes { get; init; }

    [JsonPropertyName("full_resolution_time_in_minutes")]
    public ZendeskMinutes? FullResolutionTimeInMinutes { get; init; }

    [JsonPropertyName("agent_wait_time_in_minutes")]
    public ZendeskMinutes? AgentWaitTimeInMinutes { get; init; }

    [JsonPropertyName("requester_wait_time_in_minutes")]
    public ZendeskMinutes? RequesterWaitTimeInMinutes { get; init; }

    [JsonPropertyName("on_hold_time_in_minutes")]
    public ZendeskMinutes? OnHoldTimeInMinutes { get; init; }

    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("assigned_at")] public DateTimeOffset? AssignedAt { get; init; }
    [JsonPropertyName("solved_at")] public DateTimeOffset? SolvedAt { get; init; }

    [JsonPropertyName("latest_comment_added_at")]
    public DateTimeOffset? LatestCommentAddedAt { get; init; }
}