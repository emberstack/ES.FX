using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>A page of groups (<c>GET /api/v2/groups</c>).</summary>
public sealed record ZendeskGroupsResult
{
    [JsonPropertyName("groups")] public IReadOnlyList<ZendeskGroup> Groups { get; init; } = [];
    [JsonPropertyName("count")] public int? Count { get; init; }
    [JsonPropertyName("next_page")] public string? NextPage { get; init; }
    [JsonPropertyName("previous_page")] public string? PreviousPage { get; init; }
}