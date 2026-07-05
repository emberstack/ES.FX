using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The request body for <c>POST /v1/chat/completions</c>. Only the fields the server acts on are modeled:
///     other OpenAI chat-completions fields (<c>temperature</c>, <c>max_tokens</c>, <c>tools</c>,
///     <c>tool_choice</c>, …) are silently ignored by the server — client-supplied tools are never called.
///     Unset (<c>null</c>) properties are omitted from the request.
/// </summary>
public sealed record HermesAgentChatCompletionRequest
{
    /// <summary>
    ///     The conversation messages (OpenAI chat format; required and non-empty). The last <c>user</c> or
    ///     <c>assistant</c> message is the current turn input; earlier messages become history — unless the
    ///     <c>X-Hermes-Session-Id</c> header is sent, in which case history is loaded from the server's session
    ///     store instead. The server caps message content at 65,536 characters and arrays at 1,000 items.
    /// </summary>
    [JsonPropertyName("messages")]
    public IReadOnlyList<HermesAgentChatMessage> Messages { get; init; } = [];

    /// <summary>
    ///     The optional model name. Echoed back verbatim; when it matches a configured server-side model route
    ///     alias that route's backend overrides the default. Unknown values are simply ignored.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>
    ///     Whether the server streams the completion. Controlled by the client and not settable by callers:
    ///     <see cref="Abstractions.IHermesAgentChatApi.CompleteAsync" /> forces <c>false</c> and
    ///     <see cref="Abstractions.IHermesAgentChatApi.StreamAsync" /> forces <c>true</c>.
    /// </summary>
    [JsonInclude]
    [JsonPropertyName("stream")]
    public bool? Stream { get; internal init; }
}
