using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>A Zendesk brand — decodes the <c>brand_id</c> carried on tickets (multibrand accounts).</summary>
public sealed record ZendeskBrand
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("subdomain")] public string? Subdomain { get; init; }
    [JsonPropertyName("brand_url")] public string? BrandUrl { get; init; }
    [JsonPropertyName("active")] public bool? Active { get; init; }
    [JsonPropertyName("default")] public bool? Default { get; init; }
    [JsonPropertyName("has_help_center")] public bool? HasHelpCenter { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>The <c>{ "brand": {...} }</c> envelope.</summary>
public sealed record ZendeskBrandResponse
{
    [JsonPropertyName("brand")] public ZendeskBrand? Brand { get; init; }
}

/// <summary>A page of brands (<c>{ "brands": [...] }</c> envelope). Cursor-paginated.</summary>
public sealed record ZendeskBrandsResult
{
    [JsonPropertyName("brands")] public IReadOnlyList<ZendeskBrand> Brands { get; init; } = [];
    [JsonPropertyName("count")] public int? Count { get; init; }
    [JsonPropertyName("meta")] public ZendeskCursorMeta? Meta { get; init; }
}