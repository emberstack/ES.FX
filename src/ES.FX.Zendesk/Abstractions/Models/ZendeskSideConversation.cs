using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A side conversation on a ticket — a separate thread (email/Slack/child ticket) agents use to loop in a
///     vendor or another team. Not part of the main comment thread, so it is easy to miss without this.
/// </summary>
public sealed record ZendeskSideConversation
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("ticket_id")] public long? TicketId { get; init; }
    [JsonPropertyName("subject")] public string? Subject { get; init; }

    /// <summary>A short preview of the latest message in the side conversation.</summary>
    [JsonPropertyName("preview_text")]
    public string? PreviewText { get; init; }

    /// <summary>The state (<c>open</c>, <c>closed</c>).</summary>
    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("participants")]
    public IReadOnlyList<ZendeskSideConversationParticipant>? Participants { get; init; }

    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
    [JsonPropertyName("message_added_at")] public DateTimeOffset? MessageAddedAt { get; init; }
}