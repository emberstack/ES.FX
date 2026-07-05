using System.Text.Json;
using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The <c>content</c> of a Responses API input message — either a plain string or a list of multimodal
///     content parts (the server accepts both forms and normalizes them). A <see cref="string" /> converts
///     implicitly, so <c>Content = "hello"</c> works.
/// </summary>
[JsonConverter(typeof(HermesAgentResponseInputContentJsonConverter))]
public sealed record HermesAgentResponseInputContent
{
    private HermesAgentResponseInputContent(string? text, IReadOnlyList<HermesAgentResponseInputPart>? parts)
    {
        Text = text;
        Parts = parts;
    }

    /// <summary>The plain-text form of the content; <c>null</c> when <see cref="Parts" /> is set.</summary>
    public string? Text { get; }

    /// <summary>The multimodal content-part form of the content; <c>null</c> when <see cref="Text" /> is set.</summary>
    public IReadOnlyList<HermesAgentResponseInputPart>? Parts { get; }

    /// <summary>Creates a plain-text content (serialized as a JSON string).</summary>
    public static HermesAgentResponseInputContent FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new HermesAgentResponseInputContent(text, null);
    }

    /// <summary>Creates a multimodal content (serialized as a JSON array of content parts).</summary>
    public static HermesAgentResponseInputContent FromParts(IReadOnlyList<HermesAgentResponseInputPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return new HermesAgentResponseInputContent(null, parts);
    }

    /// <summary>Converts a plain string to a text content.</summary>
    public static implicit operator HermesAgentResponseInputContent(string text) => FromText(text);
}

/// <summary>
///     Serializes <see cref="HermesAgentResponseInputContent" /> as either a JSON string or a content-part
///     array, matching the two wire forms the server accepts.
/// </summary>
internal sealed class HermesAgentResponseInputContentJsonConverter : JsonConverter<HermesAgentResponseInputContent>
{
    /// <inheritdoc />
    public override HermesAgentResponseInputContent? Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => HermesAgentResponseInputContent.FromText(reader.GetString()!),
            JsonTokenType.StartArray => HermesAgentResponseInputContent.FromParts(
                JsonSerializer.Deserialize<IReadOnlyList<HermesAgentResponseInputPart>>(ref reader, options) ?? []),
            _ => throw new JsonException("Expected a string or an array of content parts for the message content.")
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, HermesAgentResponseInputContent value,
        JsonSerializerOptions options)
    {
        if (value.Text is not null)
            writer.WriteStringValue(value.Text);
        else
            JsonSerializer.Serialize(writer, value.Parts, options);
    }
}
