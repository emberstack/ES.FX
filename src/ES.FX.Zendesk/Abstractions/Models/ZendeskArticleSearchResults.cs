using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A page of articles from Help Center search (<c>GET /api/v2/help_center/articles/search</c>). Results are the
///     lightweight <see cref="ZendeskArticleSearchResult" /> shape (snippet + metadata, no full body); fetch the
///     full body with <c>zendesk_articles_read</c>.
/// </summary>
public sealed record ZendeskArticleSearchResults
{
    [JsonPropertyName("results")] public IReadOnlyList<ZendeskArticleSearchResult> Results { get; init; } = [];
    [JsonPropertyName("count")] public int Count { get; init; }
    [JsonPropertyName("page")] public int? Page { get; init; }
    [JsonPropertyName("page_count")] public int? PageCount { get; init; }
    [JsonPropertyName("next_page")] public string? NextPage { get; init; }
    [JsonPropertyName("previous_page")] public string? PreviousPage { get; init; }
}