using System.Net.Http.Headers;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     Optional per-call request headers accepted by the Hermes Agent conversational endpoints (chat
///     completions, responses, run creation and session chat). All properties are optional; <c>null</c> values
///     are simply not sent. Values are validated when the request is built — a value containing characters
///     illegal in an HTTP header (e.g. CR/LF) throws <see cref="FormatException" /> before anything is sent.
/// </summary>
public sealed record HermesAgentRequestHeaders
{
    internal const string SessionIdHeaderName = "X-Hermes-Session-Id";
    internal const string SessionKeyHeaderName = "X-Hermes-Session-Key";
    internal const string IdempotencyKeyHeaderName = "Idempotency-Key";

    /// <summary>
    ///     The Hermes session id for session continuity (<c>X-Hermes-Session-Id</c>). The server echoes the
    ///     effective session id back on the same response header. Requires a configured server API key.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    ///     The long-term memory scoping key (<c>X-Hermes-Session-Key</c>). Echoed back verbatim on the response.
    ///     Requires a configured server API key.
    /// </summary>
    public string? SessionKey { get; init; }

    /// <summary>
    ///     The idempotency key (<c>Idempotency-Key</c>) used by the server to de-duplicate retried requests
    ///     (the request body fingerprint must also match for a cached replay).
    /// </summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>
    ///     Applies the non-empty header values to <paramref name="request" />. Values are added WITH validation
    ///     (<see cref="System.Net.Http.Headers.HttpHeaders.Add(string, string)" />), so a value containing CR/LF or
    ///     other illegal header characters throws <see cref="FormatException" /> instead of being written to the
    ///     wire — header injection through caller-supplied values is not possible.
    /// </summary>
    internal void Apply(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(SessionId))
            request.Headers.Add(SessionIdHeaderName, SessionId);
        if (!string.IsNullOrWhiteSpace(SessionKey))
            request.Headers.Add(SessionKeyHeaderName, SessionKey);
        if (!string.IsNullOrWhiteSpace(IdempotencyKey))
            request.Headers.Add(IdempotencyKeyHeaderName, IdempotencyKey);
    }

    /// <summary>
    ///     Reads the effective Hermes session id the server reported on the <c>X-Hermes-Session-Id</c> response
    ///     header (derived server-side when the request sent none; may differ from the request after session
    ///     rotation on compression). Returns <c>null</c> when the header is absent.
    /// </summary>
    internal static string? GetEffectiveSessionId(HttpResponseHeaders headers) =>
        headers.TryGetValues(SessionIdHeaderName, out var values) ? values.FirstOrDefault() : null;
}