using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>A Help Center (Guide) section — the middle tier of the category → section → article hierarchy.</summary>
public sealed record ZendeskHelpCenterSection
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("locale")] public string? Locale { get; init; }
    [JsonPropertyName("category_id")] public long? CategoryId { get; init; }
    [JsonPropertyName("position")] public long? Position { get; init; }
    [JsonPropertyName("outdated")] public bool? Outdated { get; init; }
    [JsonPropertyName("html_url")] public string? HtmlUrl { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>The <c>{ "section": {...} }</c> envelope.</summary>
public sealed record ZendeskHelpCenterSectionResponse
{
    [JsonPropertyName("section")] public ZendeskHelpCenterSection? Section { get; init; }
}

/// <summary>A page of Help Center sections (<c>{ "sections": [...] }</c> envelope).</summary>
public sealed record ZendeskHelpCenterSectionsResult
{
    [JsonPropertyName("sections")] public IReadOnlyList<ZendeskHelpCenterSection> Sections { get; init; } = [];
    [JsonPropertyName("count")] public int? Count { get; init; }
    [JsonPropertyName("next_page")] public string? NextPage { get; init; }
    [JsonPropertyName("previous_page")] public string? PreviousPage { get; init; }
    [JsonPropertyName("meta")] public ZendeskCursorMeta? Meta { get; init; }
}