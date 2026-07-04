using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A custom ticket status — decodes the <c>custom_status_id</c> carried on tickets when custom statuses are
///     enabled.
/// </summary>
public sealed record ZendeskCustomStatus
{
    [JsonPropertyName("id")] public long Id { get; init; }

    /// <summary>
    ///     The built-in category the status maps to (<c>new</c>, <c>open</c>, <c>pending</c>, <c>hold</c>, <c>solved</c>
    ///     ).
    /// </summary>
    [JsonPropertyName("status_category")]
    public string? StatusCategory { get; init; }

    /// <summary>The label agents see.</summary>
    [JsonPropertyName("agent_label")]
    public string? AgentLabel { get; init; }

    /// <summary>The label end users see.</summary>
    [JsonPropertyName("end_user_label")]
    public string? EndUserLabel { get; init; }

    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("active")] public bool? Active { get; init; }
    [JsonPropertyName("default")] public bool? Default { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>The <c>{ "custom_status": {...} }</c> envelope.</summary>
public sealed record ZendeskCustomStatusResponse
{
    [JsonPropertyName("custom_status")] public ZendeskCustomStatus? CustomStatus { get; init; }
}

/// <summary>The <c>{ "custom_statuses": [...] }</c> envelope (not paginated).</summary>
public sealed record ZendeskCustomStatusesResult
{
    [JsonPropertyName("custom_statuses")] public IReadOnlyList<ZendeskCustomStatus> CustomStatuses { get; init; } = [];
}