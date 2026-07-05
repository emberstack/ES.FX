using System.Text.Json;
using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The <c>input</c> of a Responses API request (<c>POST /v1/responses</c>) — either a plain string (a single
///     user message) or a list of input messages. A <see cref="string" /> converts implicitly, so
///     <c>Input = "hello"</c> works.
/// </summary>
[JsonConverter(typeof(HermesAgentResponseInputJsonConverter))]
public sealed record HermesAgentResponseInput
{
    private HermesAgentResponseInput(string? text, IReadOnlyList<HermesAgentResponseInputMessage>? messages)
    {
        Text = text;
        Messages = messages;
    }

    /// <summary>The plain-text form of the input; <c>null</c> when <see cref="Messages" /> is set.</summary>
    public string? Text { get; }

    /// <summary>The message-list form of the input; <c>null</c> when <see cref="Text" /> is set.</summary>
    public IReadOnlyList<HermesAgentResponseInputMessage>? Messages { get; }

    /// <summary>Creates a plain-text input (the server treats it as a single user message).</summary>
    public static HermesAgentResponseInput FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new HermesAgentResponseInput(text, null);
    }

    /// <summary>
    ///     Creates a message-list input. The last message is the current turn; earlier messages become the
    ///     conversation history (unless an explicit history or session continuation overrides them).
    /// </summary>
    public static HermesAgentResponseInput FromMessages(IReadOnlyList<HermesAgentResponseInputMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return new HermesAgentResponseInput(null, messages);
    }

    /// <summary>Converts a plain string to a text input.</summary>
    public static implicit operator HermesAgentResponseInput(string text) => FromText(text);
}

/// <summary>
///     Serializes <see cref="HermesAgentResponseInput" /> as either a JSON string or a message array, matching
///     the two wire forms the server accepts.
/// </summary>
internal sealed class HermesAgentResponseInputJsonConverter : JsonConverter<HermesAgentResponseInput>
{
    /// <inheritdoc />
    public override HermesAgentResponseInput? Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => HermesAgentResponseInput.FromText(reader.GetString()!),
            JsonTokenType.StartArray => HermesAgentResponseInput.FromMessages(
                JsonSerializer.Deserialize<IReadOnlyList<HermesAgentResponseInputMessage>>(ref reader, options) ??
                []),
            _ => throw new JsonException("Expected a string or an array of input messages for the request input.")
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, HermesAgentResponseInput value, JsonSerializerOptions options)
    {
        if (value.Text is not null)
            writer.WriteStringValue(value.Text);
        else
            JsonSerializer.Serialize(writer, value.Messages, options);
    }
}
