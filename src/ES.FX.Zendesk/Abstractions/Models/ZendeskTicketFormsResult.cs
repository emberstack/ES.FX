using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     The ticket forms returned by <c>GET /api/v2/ticket_forms.json</c>.
/// </summary>
public sealed record ZendeskTicketFormsResult
{
    /// <summary>The ticket forms.</summary>
    [JsonPropertyName("ticket_forms")]
    public IReadOnlyList<ZendeskTicketForm> TicketForms { get; init; } = [];

    /// <summary>The total number of forms, if reported.</summary>
    [JsonPropertyName("count")]
    public int? Count { get; init; }

    /// <summary>The URL of the next page of results, if any.</summary>
    [JsonPropertyName("next_page")]
    public string? NextPage { get; init; }
}