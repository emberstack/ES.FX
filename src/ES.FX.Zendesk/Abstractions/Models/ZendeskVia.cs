using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>Describes how a ticket or comment entered the system (its channel).</summary>
public sealed record ZendeskVia
{
    [JsonPropertyName("channel")] public string? Channel { get; init; }
}