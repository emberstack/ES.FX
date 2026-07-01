using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A Help Center (Guide) knowledge base article. <see cref="Body" /> is HTML. <see cref="Snippet" /> is only
///     populated on search results.
/// </summary>
public sealed record ZendeskArticle
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("html_url")] public string? HtmlUrl { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }

    /// <summary>The article body (HTML).</summary>
    [JsonPropertyName("body")]
    public string? Body { get; init; }

    /// <summary>A relevance snippet (search results only).</summary>
    [JsonPropertyName("snippet")]
    public string? Snippet { get; init; }

    [JsonPropertyName("locale")] public string? Locale { get; init; }
    [JsonPropertyName("section_id")] public long? SectionId { get; init; }
    [JsonPropertyName("author_id")] public long? AuthorId { get; init; }
    [JsonPropertyName("label_names")] public IReadOnlyList<string>? LabelNames { get; init; }
    [JsonPropertyName("draft")] public bool? Draft { get; init; }
    [JsonPropertyName("promoted")] public bool? Promoted { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
    [JsonPropertyName("edited_at")] public DateTimeOffset? EditedAt { get; init; }
}