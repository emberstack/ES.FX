using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>Envelope for a single-article response (<c>{ "article": { ... } }</c>).</summary>
public sealed record ZendeskArticleResponse
{
    [JsonPropertyName("article")] public ZendeskArticle? Article { get; init; }
}