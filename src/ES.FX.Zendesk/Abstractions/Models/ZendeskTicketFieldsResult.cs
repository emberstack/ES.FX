using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>The ticket field definitions (<c>GET /api/v2/ticket_fields</c>).</summary>
public sealed record ZendeskTicketFieldsResult
{
    [JsonPropertyName("ticket_fields")] public IReadOnlyList<ZendeskTicketField> TicketFields { get; init; } = [];
    [JsonPropertyName("count")] public int? Count { get; init; }
    [JsonPropertyName("next_page")] public string? NextPage { get; init; }
    [JsonPropertyName("previous_page")] public string? PreviousPage { get; init; }
}