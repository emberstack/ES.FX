using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A Help Center article as returned by search — the lightweight shape (title, relevance <see cref="Snippet" />
///     and metadata) without the full article body. Call <c>zendesk_articles_read</c> for the complete body.
/// </summary>
public sealed record ZendeskArticleSearchResult
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("html_url")] public string? HtmlUrl { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }

    /// <summary>A relevance snippet from the matching article.</summary>
    [JsonPropertyName("snippet")]
    public string? Snippet { get; init; }

    [JsonPropertyName("locale")] public string? Locale { get; init; }
    [JsonPropertyName("section_id")] public long? SectionId { get; init; }
    [JsonPropertyName("author_id")] public long? AuthorId { get; init; }
    [JsonPropertyName("label_names")] public IReadOnlyList<string>? LabelNames { get; init; }
    [JsonPropertyName("promoted")] public bool? Promoted { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
}