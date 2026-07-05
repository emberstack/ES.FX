using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     A streaming chat-completion chunk (<c>POST /v1/chat/completions</c> with <c>stream: true</c>). The first
///     chunk carries the assistant role delta, subsequent chunks carry content deltas, and the final chunk
///     carries an empty delta plus <see cref="HermesAgentChatCompletionChunkChoice.FinishReason" />,
///     <see cref="Usage" /> and — when the run did not finish cleanly — <see cref="Error" /> and
///     <see cref="Hermes" />.
/// </summary>
public sealed record HermesAgentChatCompletionChunk
{
    /// <summary>The completion identifier (<c>chatcmpl-…</c>; stable across the chunks of one stream).</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The object type discriminator (always <c>chat.completion.chunk</c>).</summary>
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    /// <summary>The creation time in unix seconds.</summary>
    [JsonPropertyName("created")]
    public long? Created { get; init; }

    /// <summary>The model name echoed back from the request (or the server's advertised model).</summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>The chunk choices (the server returns exactly one).</summary>
    [JsonPropertyName("choices")]
    public IReadOnlyList<HermesAgentChatCompletionChunkChoice> Choices { get; init; } = [];

    /// <summary>The token usage — present only on the final chunk.</summary>
    [JsonPropertyName("usage")]
    public HermesAgentChatUsage? Usage { get; init; }

    /// <summary>
    ///     The error carried by the final chunk when the run ended with an error message (only
    ///     <see cref="HermesAgentError.Message" /> and <see cref="HermesAgentError.Type" /> are populated on this
    ///     path).
    /// </summary>
    [JsonPropertyName("error")]
    public HermesAgentError? Error { get; init; }

    /// <summary>
    ///     The Hermes extension status object — present on the final chunk when the run did not finish cleanly
    ///     (<see cref="HermesAgentChatCompletionChunkChoice.FinishReason" /> other than <c>stop</c>).
    /// </summary>
    [JsonPropertyName("hermes")]
    public HermesAgentHermesStatus? Hermes { get; init; }
}

/// <summary>
///     A single choice of a streaming chat-completion chunk.
/// </summary>
public sealed record HermesAgentChatCompletionChunkChoice
{
    /// <summary>The choice index (the server always returns <c>0</c>).</summary>
    [JsonPropertyName("index")]
    public int Index { get; init; }

    /// <summary>The incremental message delta (empty on the final chunk).</summary>
    [JsonPropertyName("delta")]
    public HermesAgentChatMessageDelta? Delta { get; init; }

    /// <summary>
    ///     Why generation stopped — <c>null</c> until the final chunk, then a value from
    ///     <see cref="HermesAgentChatFinishReasons" />.
    /// </summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}

/// <summary>
///     The incremental delta of a streaming chat-completion chunk: the assistant role on the first chunk, a
///     content fragment on text chunks, and empty on the final chunk.
/// </summary>
public sealed record HermesAgentChatMessageDelta
{
    /// <summary>The message role (<c>assistant</c>; sent on the first chunk only).</summary>
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    /// <summary>The incremental content text.</summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }
}
