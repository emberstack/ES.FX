using System.Text.Json;
using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The <c>output</c> of a <c>function_call_output</c> item. The server sends a plain string on the
///     non-streaming path but an <c>input_text</c> part array in streaming terminal payloads — consumers must
///     check <see cref="Text" /> first and fall back to <see cref="Parts" />.
/// </summary>
[JsonConverter(typeof(HermesAgentResponseFunctionCallOutputJsonConverter))]
public sealed record HermesAgentResponseFunctionCallOutput
{
    private HermesAgentResponseFunctionCallOutput(string? text, IReadOnlyList<HermesAgentResponseContentPart>? parts)
    {
        Text = text;
        Parts = parts;
    }

    /// <summary>The plain-string form (non-streaming responses); <c>null</c> when <see cref="Parts" /> is set.</summary>
    public string? Text { get; }

    /// <summary>The part-array form (streaming terminal payloads); <c>null</c> when <see cref="Text" /> is set.</summary>
    public IReadOnlyList<HermesAgentResponseContentPart>? Parts { get; }

    /// <summary>Creates a plain-string output (the non-streaming wire form).</summary>
    public static HermesAgentResponseFunctionCallOutput FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new HermesAgentResponseFunctionCallOutput(text, null);
    }

    /// <summary>Creates a part-array output (the streaming wire form).</summary>
    public static HermesAgentResponseFunctionCallOutput FromParts(
        IReadOnlyList<HermesAgentResponseContentPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return new HermesAgentResponseFunctionCallOutput(null, parts);
    }
}

/// <summary>
///     Deserializes <see cref="HermesAgentResponseFunctionCallOutput" /> from either wire form (a JSON string or
///     a content-part array) and serializes it back to the form it was created from.
/// </summary>
internal sealed class HermesAgentResponseFunctionCallOutputJsonConverter
    : JsonConverter<HermesAgentResponseFunctionCallOutput>
{
    /// <inheritdoc />
    public override HermesAgentResponseFunctionCallOutput? Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => HermesAgentResponseFunctionCallOutput.FromText(reader.GetString()!),
            JsonTokenType.StartArray => HermesAgentResponseFunctionCallOutput.FromParts(
                JsonSerializer.Deserialize<IReadOnlyList<HermesAgentResponseContentPart>>(ref reader, options) ??
                []),
            _ => throw new JsonException("Expected a string or an array for the function call output.")
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, HermesAgentResponseFunctionCallOutput value,
        JsonSerializerOptions options)
    {
        if (value.Text is not null)
            writer.WriteStringValue(value.Text);
        else
            JsonSerializer.Serialize(writer, value.Parts, options);
    }
}