using System.Text.Json;
using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The <c>input</c> of a run request (<c>POST /v1/runs</c>) — on the wire either a plain string (a single
///     user message) or an array of role/content messages whose last item becomes the current-turn user
///     message. Exactly one of <see cref="Text" /> and <see cref="Messages" /> is set; a string converts
///     implicitly for the common case.
/// </summary>
[JsonConverter(typeof(HermesAgentRunInputJsonConverter))]
public sealed record HermesAgentRunInput
{
    private HermesAgentRunInput(string? text, IReadOnlyList<HermesAgentRunMessage>? messages)
    {
        Text = text;
        Messages = messages;
    }

    /// <summary>The plain-text user message, when constructed from text; otherwise <c>null</c>.</summary>
    public string? Text { get; }

    /// <summary>The message list, when constructed from messages; otherwise <c>null</c>.</summary>
    public IReadOnlyList<HermesAgentRunMessage>? Messages { get; }

    /// <summary>Creates an input carrying a single plain-text user message (serialized as a JSON string).</summary>
    public static HermesAgentRunInput FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new HermesAgentRunInput(text, null);
    }

    /// <summary>
    ///     Creates an input carrying a message list (serialized as a JSON array). The last item's content
    ///     becomes the current-turn user message; earlier items become history unless
    ///     <see cref="HermesAgentRunRequest.ConversationHistory" /> is supplied.
    /// </summary>
    public static HermesAgentRunInput FromMessages(IReadOnlyList<HermesAgentRunMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return new HermesAgentRunInput(null, messages);
    }

    /// <summary>Converts a plain string to a text input (see <see cref="FromText" />).</summary>
    public static implicit operator HermesAgentRunInput(string text) => FromText(text);
}

/// <summary>
///     Serializes <see cref="HermesAgentRunInput" /> as either a JSON string or a JSON array of messages,
///     matching the union the server accepts for the run <c>input</c> field.
/// </summary>
internal sealed class HermesAgentRunInputJsonConverter : JsonConverter<HermesAgentRunInput>
{
    /// <inheritdoc />
    public override HermesAgentRunInput? Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        // The client never deserializes run requests, but the converter round-trips for completeness.
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return HermesAgentRunInput.FromText(reader.GetString() ?? string.Empty);
            case JsonTokenType.StartArray:
                var messages = JsonSerializer.Deserialize<List<HermesAgentRunMessage>>(ref reader, options);
                return messages is null ? null : HermesAgentRunInput.FromMessages(messages);
            default:
                throw new JsonException("Expected a string or an array of messages for the run 'input'.");
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, HermesAgentRunInput value, JsonSerializerOptions options)
    {
        // The factories guarantee exactly one side of the union is set.
        if (value.Text is not null)
            writer.WriteStringValue(value.Text);
        else
            JsonSerializer.Serialize(writer, value.Messages, options);
    }
}