using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A membership linking an agent (<see cref="UserId" />) to a group (<see cref="GroupId" />). Use to enumerate
///     the agents who belong to a group (e.g. valid assignment targets).
/// </summary>
public sealed record ZendeskGroupMembership
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("user_id")] public long? UserId { get; init; }
    [JsonPropertyName("group_id")] public long? GroupId { get; init; }

    /// <summary>Whether this is the user's default group.</summary>
    [JsonPropertyName("default")]
    public bool? Default { get; init; }

    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
}