using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>A duration reported by Zendesk metrics in both business and calendar hours.</summary>
public sealed record ZendeskMinutes
{
    [JsonPropertyName("business")] public int? Business { get; init; }
    [JsonPropertyName("calendar")] public int? Calendar { get; init; }
}