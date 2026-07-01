using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>An attachment on a Zendesk ticket comment.</summary>
public sealed record ZendeskAttachment
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("file_name")] public string? FileName { get; init; }
    [JsonPropertyName("content_url")] public string? ContentUrl { get; init; }
    [JsonPropertyName("content_type")] public string? ContentType { get; init; }
    [JsonPropertyName("size")] public long? Size { get; init; }
    [JsonPropertyName("inline")] public bool? Inline { get; init; }
}