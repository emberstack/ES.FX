using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>A page of ticket comments (<c>GET /api/v2/tickets/{id}/comments.json</c>).</summary>
public sealed record ZendeskTicketCommentsResult
{
    [JsonPropertyName("comments")] public IReadOnlyList<ZendeskTicketComment> Comments { get; init; } = [];
    [JsonPropertyName("count")] public int? Count { get; init; }
    [JsonPropertyName("next_page")] public string? NextPage { get; init; }
    [JsonPropertyName("previous_page")] public string? PreviousPage { get; init; }
}