using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>A tag with its usage count (from <c>GET /api/v2/tags.json</c>).</summary>
public sealed record ZendeskTag
{
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("count")] public long? Count { get; init; }
}

/// <summary>
///     A page of tags with usage counts (<c>{ "tags": [ { "name": ..., "count": ... } ] }</c> envelope). Zendesk
///     returns up to the 20,000 most popular tags of the last 60 days, in decreasing popularity.
/// </summary>
public sealed record ZendeskTagsResult
{
    [JsonPropertyName("tags")] public IReadOnlyList<ZendeskTag> Tags { get; init; } = [];
    [JsonPropertyName("count")] public int? Count { get; init; }
    [JsonPropertyName("next_page")] public string? NextPage { get; init; }
    [JsonPropertyName("previous_page")] public string? PreviousPage { get; init; }
    [JsonPropertyName("meta")] public ZendeskCursorMeta? Meta { get; init; }
}

/// <summary>
///     Tag-name suggestions (<c>{ "tags": [ "name", ... ] }</c> envelope from
///     <c>GET /api/v2/autocomplete/tags.json</c> — plain strings, unlike <see cref="ZendeskTagsResult" />).
/// </summary>
public sealed record ZendeskTagNamesResult
{
    [JsonPropertyName("tags")] public IReadOnlyList<string> Tags { get; init; } = [];
}