using System.Net;

namespace ES.FX.Zendesk;

/// <summary>
///     Thrown when the Zendesk API returns a non-success status code. Carries the status code and (best-effort)
///     the raw response body so callers can surface the actual Zendesk error instead of an opaque
///     <see cref="HttpRequestException" />.
/// </summary>
public sealed class ZendeskApiException : Exception
{
    /// <summary>Creates a new <see cref="ZendeskApiException" />.</summary>
    public ZendeskApiException(HttpStatusCode statusCode, string? responseBody, string message) : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    /// <summary>The HTTP status code returned by Zendesk.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>The raw response body returned by Zendesk (truncated), if any.</summary>
    public string? ResponseBody { get; }

    /// <summary>
    ///     The server-requested wait before retrying, from the <c>Retry-After</c> header (typically sent with
    ///     <c>429 Too Many Requests</c>), if any. A date-form header is converted to a delay when the response is
    ///     read; past dates clamp to <see cref="TimeSpan.Zero" />.
    /// </summary>
    public TimeSpan? RetryAfter { get; init; }
}