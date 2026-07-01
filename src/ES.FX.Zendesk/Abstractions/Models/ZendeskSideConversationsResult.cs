using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>A page of side conversations (<c>GET /api/v2/tickets/{id}/side_conversations</c>).</summary>
public sealed record ZendeskSideConversationsResult
{
    [JsonPropertyName("side_conversations")]
    public IReadOnlyList<ZendeskSideConversation> SideConversations { get; init; } = [];

    [JsonPropertyName("count")] public int? Count { get; init; }
    [JsonPropertyName("next_page")] public string? NextPage { get; init; }
    [JsonPropertyName("previous_page")] public string? PreviousPage { get; init; }
}