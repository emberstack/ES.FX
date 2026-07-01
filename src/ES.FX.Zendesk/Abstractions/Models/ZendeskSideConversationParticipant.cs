using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>A participant on a side conversation (an internal/vendor/escalation thread off a ticket).</summary>
public sealed record ZendeskSideConversationParticipant
{
    [JsonPropertyName("user_id")] public long? UserId { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("email")] public string? Email { get; init; }
}