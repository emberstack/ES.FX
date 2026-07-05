using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     A multimodal content part of a Responses API input message (<c>POST /v1/responses</c>). The concrete
///     part type is serialized as the wire <c>type</c> discriminator (<c>input_text</c> or <c>input_image</c>).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(HermesAgentResponseInputTextPart), "input_text")]
[JsonDerivedType(typeof(HermesAgentResponseInputImagePart), "input_image")]
public abstract record HermesAgentResponseInputPart;

/// <summary>A text content part (<c>{"type": "input_text", "text": ...}</c>).</summary>
public sealed record HermesAgentResponseInputTextPart : HermesAgentResponseInputPart
{
    /// <summary>The text content. The server truncates text at 65,536 characters.</summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

/// <summary>
///     An image content part (<c>{"type": "input_image", "image_url": ...}</c>). The URL must use the
///     <c>http</c>, <c>https</c> or <c>data:image/...</c> scheme — anything else is rejected by the server with
///     a <c>400</c> (<c>invalid_image_url</c> / <c>unsupported_content_type</c>).
/// </summary>
public sealed record HermesAgentResponseInputImagePart : HermesAgentResponseInputPart
{
    /// <summary>The image URL. Unlike the chat-completions form, this is a plain URL string.</summary>
    [JsonPropertyName("image_url")]
    public required string ImageUrl { get; init; }

    /// <summary>
    ///     The optional detail hint (e.g. <c>low</c>, <c>high</c>, <c>auto</c>). When set it must be a non-blank
    ///     string or the server rejects the part with a <c>400</c> (<c>invalid_content_part</c>).
    /// </summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; init; }
}
