using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A page of Help Center articles from list endpoints (<c>{ "articles": [...] }</c> envelope — unlike
///     article search, which wraps results in <c>results</c>).
/// </summary>
public sealed record ZendeskArticlesResult
{
    [JsonPropertyName("articles")] public IReadOnlyList<ZendeskArticle> Articles { get; init; } = [];
    [JsonPropertyName("count")] public int? Count { get; init; }
    [JsonPropertyName("next_page")] public string? NextPage { get; init; }
    [JsonPropertyName("previous_page")] public string? PreviousPage { get; init; }
    [JsonPropertyName("meta")] public ZendeskCursorMeta? Meta { get; init; }

    /// <summary>Sideloaded article authors (populated only when the request asks to include <c>users</c>).</summary>
    [JsonPropertyName("users")]
    public IReadOnlyList<ZendeskUser>? Users { get; init; }

    /// <summary>Sideloaded sections (populated only when the request asks to include <c>sections</c>).</summary>
    [JsonPropertyName("sections")]
    public IReadOnlyList<ZendeskHelpCenterSection>? Sections { get; init; }

    /// <summary>Sideloaded categories (populated only when the request asks to include <c>categories</c>).</summary>
    [JsonPropertyName("categories")]
    public IReadOnlyList<ZendeskHelpCenterCategory>? Categories { get; init; }
}