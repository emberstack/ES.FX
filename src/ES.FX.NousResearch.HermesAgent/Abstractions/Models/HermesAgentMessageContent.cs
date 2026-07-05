using System.Text.Json;
using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The content of a chat message — on the wire either a plain string or an array of
///     <see cref="HermesAgentMessageContentPart" /> items (multimodal). Exactly one of <see cref="Text" /> and
///     <see cref="Parts" /> is set. A <see cref="string" /> converts implicitly, so
///     <c>Content = "Hello"</c> works; use <see cref="FromParts(IReadOnlyList{HermesAgentMessageContentPart})" />
///     for multimodal content.
/// </summary>
[JsonConverter(typeof(HermesAgentMessageContentJsonConverter))]
public sealed record HermesAgentMessageContent
{
    private HermesAgentMessageContent(string? text, IReadOnlyList<HermesAgentMessageContentPart>? parts)
    {
        Text = text;
        Parts = parts;
    }

    /// <summary>The plain-string content, when the content is (or was sent as) a single string.</summary>
    public string? Text { get; }

    /// <summary>The content parts, when the content is (or was sent as) a multimodal array.</summary>
    public IReadOnlyList<HermesAgentMessageContentPart>? Parts { get; }

    /// <summary>Creates plain-string content (serialized as a JSON string).</summary>
    public static HermesAgentMessageContent FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new HermesAgentMessageContent(text, null);
    }

    /// <summary>Creates multimodal content (serialized as a JSON array of content parts).</summary>
    public static HermesAgentMessageContent FromParts(params IReadOnlyList<HermesAgentMessageContentPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return new HermesAgentMessageContent(null, parts);
    }

    /// <summary>Converts a plain string to <see cref="HermesAgentMessageContent" /> (see <see cref="FromText" />).</summary>
    public static implicit operator HermesAgentMessageContent(string text) => FromText(text);
}

/// <summary>
///     Serializes <see cref="HermesAgentMessageContent" /> as either a JSON string or a content-part array, and
///     reads both wire shapes back (bare strings inside an array are accepted as text parts, mirroring the
///     server's leniency).
/// </summary>
internal sealed class HermesAgentMessageContentJsonConverter : JsonConverter<HermesAgentMessageContent>
{
    /// <inheritdoc />
    public override HermesAgentMessageContent Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return HermesAgentMessageContent.FromText(reader.GetString() ?? string.Empty);

            case JsonTokenType.StartArray:
                {
                    var parts = new List<HermesAgentMessageContentPart>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            parts.Add(HermesAgentMessageContentPart.FromText(reader.GetString() ?? string.Empty));
                            continue;
                        }

                        var part = JsonSerializer.Deserialize<HermesAgentMessageContentPart>(ref reader, options);
                        if (part is not null)
                            parts.Add(part);
                    }

                    return HermesAgentMessageContent.FromParts(parts);
                }

            default:
                throw new JsonException(
                    $"Unexpected JSON token '{reader.TokenType}' for a chat message content value.");
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, HermesAgentMessageContent value,
        JsonSerializerOptions options)
    {
        if (value.Text is not null)
            writer.WriteStringValue(value.Text);
        else
            JsonSerializer.Serialize(writer, value.Parts ?? [], options);
    }
}
