using System.Net;
using ES.FX.NousResearch.HermesAgent.Abstractions.Models;

namespace ES.FX.NousResearch.HermesAgent;

/// <summary>
///     Thrown when the Hermes Agent API returns a non-success status code. Carries the status code, the parsed
///     error object (when the body carried one) and (best-effort) the raw response body so callers can surface
///     the actual server error instead of an opaque <see cref="HttpRequestException" />.
/// </summary>
public sealed class HermesAgentApiException : Exception
{
    /// <summary>Creates a new <see cref="HermesAgentApiException" />.</summary>
    public HermesAgentApiException(HttpStatusCode statusCode, string? responseBody, string message) : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    /// <summary>The HTTP status code returned by the Hermes Agent API.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>The raw response body returned by the Hermes Agent API (truncated), if any.</summary>
    public string? ResponseBody { get; }

    /// <summary>
    ///     The parsed error object, if the response body carried one. Both the OpenAI-style envelope
    ///     (<c>{"error": {...}}</c>) and the jobs API's flat <c>{"error": "&lt;string&gt;"}</c> shape are parsed;
    ///     for the flat shape only <see cref="HermesAgentError.Message" /> is set.
    /// </summary>
    public HermesAgentError? Error { get; init; }

    /// <summary>
    ///     The server-requested wait before retrying, from the <c>Retry-After</c> header (sent with
    ///     <c>429 Too Many Requests</c> when the concurrent-run cap is hit), if any. A date-form header is
    ///     converted to a delay when the response is read; past dates clamp to <see cref="TimeSpan.Zero" />.
    /// </summary>
    public TimeSpan? RetryAfter { get; init; }
}
