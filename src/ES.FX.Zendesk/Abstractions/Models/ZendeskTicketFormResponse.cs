using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     Envelope for a single ticket-form Zendesk response (<c>{ "ticket_form": { ... } }</c>).
/// </summary>
public sealed record ZendeskTicketFormResponse
{
    /// <summary>The ticket form payload.</summary>
    [JsonPropertyName("ticket_form")]
    public ZendeskTicketForm? TicketForm { get; init; }
}