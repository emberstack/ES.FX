using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     A plain-text conversation message used by the runs endpoint — both as an item of a message-list
///     <see cref="HermesAgentRunInput" /> and as an entry of
///     <see cref="HermesAgentRunRequest.ConversationHistory" />. The server requires both fields on history
///     entries (<c>400</c> otherwise); <c>null</c> values are omitted from the request.
/// </summary>
public sealed record HermesAgentRunMessage
{
    /// <summary>The message role (e.g. <c>user</c>, <c>assistant</c>, <c>system</c>).</summary>
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    /// <summary>The plain-text message content (the runs endpoint performs no multimodal normalization).</summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }
}
