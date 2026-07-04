using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>The ticket field definitions (<c>GET /api/v2/ticket_fields</c>).</summary>
public sealed record ZendeskTicketFieldsResult
{
    [JsonPropertyName("ticket_fields")] public IReadOnlyList<ZendeskTicketField> TicketFields { get; init; } = [];
    [JsonPropertyName("count")] public int? Count { get; init; }
    [JsonPropertyName("next_page")] public string? NextPage { get; init; }
    [JsonPropertyName("previous_page")] public string? PreviousPage { get; init; }

    /// <summary>Sideloaded field creators (populated only when the request asks to include <c>users</c>).</summary>
    [JsonPropertyName("users")]
    public IReadOnlyList<ZendeskUser>? Users { get; init; }

    /// <summary>Cursor-pagination metadata (populated when the request used cursor pagination).</summary>
    [JsonPropertyName("meta")]
    public ZendeskCursorMeta? Meta { get; init; }
}