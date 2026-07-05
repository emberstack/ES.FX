using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     A single content part of a multimodal message (chat completions and session chat). The server accepts
///     text parts (<c>{"type":"text","text":…}</c>; the aliases <c>input_text</c>/<c>output_text</c> are also
///     accepted) and image parts (<c>{"type":"image_url","image_url":{"url":…,"detail":…}}</c>; the Responses
///     form <c>input_image</c> is also accepted). The part type is deliberately a string — unknown values are
///     rejected by the server, not by the client. Use <see cref="FromText" /> / <see cref="FromImageUrl" /> to
///     build well-formed parts.
/// </summary>
public sealed record HermesAgentMessageContentPart
{
    private const string TextType = "text";
    private const string ImageUrlType = "image_url";

    /// <summary>The part type (<c>text</c> or <c>image_url</c> in the canonical chat form).</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The text payload (text parts only; the server truncates at 65,536 characters).</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>The image payload (image parts only).</summary>
    [JsonPropertyName("image_url")]
    public HermesAgentImageUrl? ImageUrl { get; init; }

    /// <summary>Creates a <c>text</c> content part.</summary>
    public static HermesAgentMessageContentPart FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new HermesAgentMessageContentPart { Type = TextType, Text = text };
    }

    /// <summary>
    ///     Creates an <c>image_url</c> content part. The server accepts <c>http://</c>, <c>https://</c> and
    ///     <c>data:image/…</c> URLs (anything else is rejected with <c>400 invalid_image_url</c>).
    /// </summary>
    public static HermesAgentMessageContentPart FromImageUrl(string url, string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        return new HermesAgentMessageContentPart
        {
            Type = ImageUrlType,
            ImageUrl = new HermesAgentImageUrl { Url = url, Detail = detail }
        };
    }
}

/// <summary>
///     The image payload of an <c>image_url</c> content part.
/// </summary>
public sealed record HermesAgentImageUrl
{
    /// <summary>
    ///     The image URL — <c>http://</c>, <c>https://</c> or a <c>data:image/…</c> data URL (the server rejects
    ///     other schemes and non-image data URLs).
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    /// <summary>The optional detail hint (a non-blank string; the server rejects blank values).</summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; init; }
}
