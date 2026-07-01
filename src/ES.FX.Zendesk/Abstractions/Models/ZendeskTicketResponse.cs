using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     Envelope for a single-ticket Zendesk response (<c>{ "ticket": { ... } }</c>).
/// </summary>
public sealed record ZendeskTicketResponse
{
    /// <summary>The ticket payload.</summary>
    [JsonPropertyName("ticket")]
    public ZendeskTicket? Ticket { get; init; }
}