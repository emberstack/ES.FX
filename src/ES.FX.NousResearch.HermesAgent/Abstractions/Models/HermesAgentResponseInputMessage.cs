using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     An input message of a Responses API request (<c>POST /v1/responses</c>), used both for the message-list
///     form of <see cref="HermesAgentResponseRequest.Input" /> (last message is the current turn, earlier ones
///     become history) and for <see cref="HermesAgentResponseRequest.ConversationHistory" /> items.
/// </summary>
public sealed record HermesAgentResponseInputMessage
{
    /// <summary>
    ///     The message role (e.g. <c>user</c>, <c>assistant</c>, <c>system</c>). Defaults to <c>user</c> — the
    ///     value is always sent because <c>conversation_history</c> items are rejected without an explicit role.
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; init; } = "user";

    /// <summary>The message content — a plain string or multimodal content parts.</summary>
    [JsonPropertyName("content")]
    public required HermesAgentResponseInputContent Content { get; init; }
}
