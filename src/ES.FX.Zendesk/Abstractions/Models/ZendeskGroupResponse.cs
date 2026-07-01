using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>Envelope for a single-group response (<c>{ "group": { ... } }</c>).</summary>
public sealed record ZendeskGroupResponse
{
    [JsonPropertyName("group")] public ZendeskGroup? Group { get; init; }
}