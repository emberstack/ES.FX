using ES.FX.NousResearch.HermesAgent.Abstractions.Models;

namespace ES.FX.NousResearch.HermesAgent.Abstractions;

/// <summary>
///     Operations against the OpenAI-compatible chat completions endpoint (<c>POST /v1/chat/completions</c>).
/// </summary>
public interface IHermesAgentChatApi
{
    /// <summary>
    ///     Creates a chat completion (<c>POST /v1/chat/completions</c>; <c>stream</c> is forced to <c>false</c>).
    ///     A degraded-but-usable agent run still returns <c>200</c> with
    ///     <see cref="HermesAgentChatCompletion.Hermes" /> set and a finish reason of <c>length</c> or
    ///     <c>error</c>; a run with no usable text fails as <c>502</c> (<see cref="HermesAgentApiException" />
    ///     with error code <c>agent_incomplete</c>). Optional <paramref name="headers" /> carry session
    ///     continuity (<c>X-Hermes-Session-Id</c>), memory scoping (<c>X-Hermes-Session-Key</c>) and the
    ///     <c>Idempotency-Key</c> honored by this non-streaming path (300 s in-memory window). The effective
    ///     session id the server reports on the <c>X-Hermes-Session-Id</c> response header (derived when the
    ///     request sent none, possibly rotated on compression) is surfaced as
    ///     <see cref="HermesAgentChatCompletion.EffectiveSessionId" />.
    /// </summary>
    Task<HermesAgentChatCompletion> CompleteAsync(HermesAgentChatCompletionRequest request,
        HermesAgentRequestHeaders? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Streams a chat completion (<c>POST /v1/chat/completions</c> with <c>stream</c> forced to <c>true</c>).
    ///     Yields <see cref="HermesAgentChatCompletionChunkEvent" /> for the OpenAI-style chunks (role delta,
    ///     content deltas, then a final chunk with finish reason and usage) interleaved with
    ///     <see cref="HermesAgentToolProgressEvent" /> for the named <c>hermes.tool.progress</c> events;
    ///     unrecognized events surface as <see cref="HermesAgentChatStreamUnknownEvent" /> and never fail the
    ///     stream. Enumeration ends at the server's <c>data: [DONE]</c> terminator; nothing is sent until
    ///     enumeration starts. <c>Idempotency-Key</c> is NOT honored on this streaming path. When the server
    ///     reports the effective session id on the <c>X-Hermes-Session-Id</c> response header, a synthetic
    ///     <see cref="HermesAgentChatStreamStartEvent" /> carrying it is yielded before any server event.
    /// </summary>
    IAsyncEnumerable<HermesAgentChatStreamEvent> StreamAsync(HermesAgentChatCompletionRequest request,
        HermesAgentRequestHeaders? headers = null, CancellationToken cancellationToken = default);
}
