using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools.Models;

/// <summary>A view condition (see Zendesk's conditions reference for the field/operator vocabulary).</summary>
public sealed record ZendeskViewCondition
{
    [JsonPropertyName("field")] public string? Field { get; init; }
    [JsonPropertyName("operator")] public string? Operator { get; init; }
    [JsonPropertyName("value")] public string? Value { get; init; }
}