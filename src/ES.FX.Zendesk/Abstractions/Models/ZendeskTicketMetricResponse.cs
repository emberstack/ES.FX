using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>Envelope for a single ticket-metric response (<c>{ "ticket_metric": { ... } }</c>).</summary>
public sealed record ZendeskTicketMetricResponse
{
    [JsonPropertyName("ticket_metric")] public ZendeskTicketMetric? TicketMetric { get; init; }
}