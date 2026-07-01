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
}