using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     The downloaded content of a Zendesk attachment. <see cref="Encoding" /> is <c>utf-8</c> when the attachment
///     is text (its <see cref="Content" /> is the decoded text) or <c>base64</c> for binary (its
///     <see cref="Content" /> is base64). <see cref="Truncated" /> is <c>true</c> when the payload exceeded the
///     server's size cap and was cut short.
/// </summary>
public sealed record ZendeskAttachmentContent
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("file_name")] public string? FileName { get; init; }
    [JsonPropertyName("content_type")] public string? ContentType { get; init; }
    [JsonPropertyName("size")] public long? Size { get; init; }
    [JsonPropertyName("encoding")] public string Encoding { get; init; } = "utf-8";
    [JsonPropertyName("content")] public string Content { get; init; } = string.Empty;
    [JsonPropertyName("truncated")] public bool Truncated { get; init; }
}