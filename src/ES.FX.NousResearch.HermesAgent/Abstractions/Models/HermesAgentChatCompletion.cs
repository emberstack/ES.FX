using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     A non-streaming chat completion (see <c>POST /v1/chat/completions</c>). A degraded-but-usable run
///     (partial/failed with some text) is still returned as <c>200</c> — inspect <see cref="Hermes" /> and the
///     choice's <see cref="HermesAgentChatCompletionChoice.FinishReason" /> to detect it; a run with no usable
///     text fails as <c>502</c> via <see cref="HermesAgentApiException" />.
/// </summary>
public sealed record HermesAgentChatCompletion
{
    /// <summary>The completion identifier (<c>chatcmpl-…</c>).</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The object type discriminator (always <c>chat.completion</c>).</summary>
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    /// <summary>The creation time in unix seconds.</summary>
    [JsonPropertyName("created")]
    public long? Created { get; init; }

    /// <summary>The model name echoed back from the request (or the server's advertised model).</summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>The completion choices (the server returns exactly one).</summary>
    [JsonPropertyName("choices")]
    public IReadOnlyList<HermesAgentChatCompletionChoice> Choices { get; init; } = [];

    /// <summary>The token usage for the request.</summary>
    [JsonPropertyName("usage")]
    public HermesAgentChatUsage? Usage { get; init; }

    /// <summary>
    ///     The Hermes extension status object — present only when the agent run degraded (soft-partial). The same
    ///     information is mirrored on the <c>X-Hermes-Completed</c>/<c>X-Hermes-Partial</c>/<c>X-Hermes-Error</c>
    ///     response headers.
    /// </summary>
    [JsonPropertyName("hermes")]
    public HermesAgentHermesStatus? Hermes { get; init; }

    /// <summary>
    ///     The effective Hermes session id, read from the <c>X-Hermes-Session-Id</c> response header (not part
    ///     of the JSON body). The server derives it when the request sent no
    ///     <see cref="HermesAgentRequestHeaders.SessionId" /> and may rotate it (differing from the request)
    ///     after session compression — send this value on the next turn to continue the conversation
    ///     server-side. <c>null</c> when the server did not report one.
    /// </summary>
    [JsonIgnore]
    public string? EffectiveSessionId { get; init; }
}

/// <summary>
///     A single chat completion choice.
/// </summary>
public sealed record HermesAgentChatCompletionChoice
{
    /// <summary>The choice index (the server always returns <c>0</c>).</summary>
    [JsonPropertyName("index")]
    public int Index { get; init; }

    /// <summary>
    ///     The assistant message. Content is always a plain string; <c>MEDIA:</c> tags referencing safe local
    ///     images are inlined by the server as base64 data-URL markdown.
    /// </summary>
    [JsonPropertyName("message")]
    public HermesAgentChatMessage? Message { get; init; }

    /// <summary>
    ///     Why generation stopped — see <see cref="HermesAgentChatFinishReasons" /> (<c>error</c> is a Hermes
    ///     extension to the OpenAI vocabulary).
    /// </summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}
