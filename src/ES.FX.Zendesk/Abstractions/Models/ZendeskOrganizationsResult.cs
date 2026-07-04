using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>A page of organizations (<c>{ "organizations": [...] }</c> envelope).</summary>
public sealed record ZendeskOrganizationsResult
{
    [JsonPropertyName("organizations")] public IReadOnlyList<ZendeskOrganization> Organizations { get; init; } = [];
    [JsonPropertyName("count")] public int? Count { get; init; }
    [JsonPropertyName("next_page")] public string? NextPage { get; init; }
    [JsonPropertyName("previous_page")] public string? PreviousPage { get; init; }

    /// <summary>Cursor-pagination metadata (populated when the request used cursor pagination).</summary>
    [JsonPropertyName("meta")]
    public ZendeskCursorMeta? Meta { get; init; }
}