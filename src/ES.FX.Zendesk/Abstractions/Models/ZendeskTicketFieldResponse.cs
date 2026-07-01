using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>Envelope for a single ticket-field response (<c>{ "ticket_field": { ... } }</c>).</summary>
public sealed record ZendeskTicketFieldResponse
{
    [JsonPropertyName("ticket_field")] public ZendeskTicketField? TicketField { get; init; }
}