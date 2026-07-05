using ES.FX.NousResearch.HermesAgent.Abstractions.Models;

namespace ES.FX.NousResearch.HermesAgent.Abstractions;

/// <summary>
///     Operations against the OpenAI-style Responses API (<c>/v1/responses</c>).
/// </summary>
public interface IHermesAgentResponsesApi
{
    /// <summary>
    ///     Creates a response without streaming (<c>POST /v1/responses</c>; <c>stream: false</c> is enforced by
    ///     the client). The returned envelope is always <c>completed</c> — agent-side errors surface inside the
    ///     final message text, while a raised server exception yields a <c>500</c>
    ///     <see cref="HermesAgentApiException" />. Stored responses (server default <c>store: true</c>) are kept
    ///     in a 100-entry LRU store for retrieval and chaining. An
    ///     <see cref="HermesAgentRequestHeaders.IdempotencyKey" /> is honored on this non-streaming path only.
    ///     The effective session id the server reports on the <c>X-Hermes-Session-Id</c> response header is
    ///     surfaced as <see cref="HermesAgentResponse.EffectiveSessionId" />.
    /// </summary>
    Task<HermesAgentResponse> CreateAsync(HermesAgentResponseRequest request,
        HermesAgentRequestHeaders? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a response and streams its typed server-sent events (<c>POST /v1/responses</c>;
    ///     <c>stream: true</c> is enforced by the client). The stream ends with a terminal
    ///     <see cref="HermesAgentResponseCompletedEvent" /> or <see cref="HermesAgentResponseFailedEvent" />;
    ///     unrecognized event types are surfaced as <see cref="HermesAgentResponseUnknownEvent" /> and never
    ///     fail the stream. Nothing is sent until enumeration starts. When the server reports the effective
    ///     session id on the <c>X-Hermes-Session-Id</c> response header, a synthetic
    ///     <see cref="HermesAgentResponseStreamStartEvent" /> carrying it is yielded before any server event.
    /// </summary>
    IAsyncEnumerable<HermesAgentResponseStreamEvent> StreamAsync(HermesAgentResponseRequest request,
        HermesAgentRequestHeaders? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a stored response by id (<c>GET /v1/responses/{response_id}</c>). Unknown or LRU-evicted ids
    ///     yield a <c>404</c>; retrieval refreshes the entry's LRU position.
    /// </summary>
    Task<HermesAgentResponse> GetByIdAsync(string responseId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a stored response (<c>DELETE /v1/responses/{response_id}</c>). Unknown ids yield a
    ///     <c>404</c>.
    /// </summary>
    Task DeleteAsync(string responseId, CancellationToken cancellationToken = default);
}