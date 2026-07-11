using System.Net;
using JetBrains.Annotations;

namespace ES.FX.OpenData.Vies;

/// <summary>
///     Thrown when the VIES service returns an infrastructure fault (non-success HTTP, an unreadable body, or a
///     documented service fault such as <c>SERVICE_UNAVAILABLE</c> / <c>*_MAX_CONCURRENT_REQ</c> / <c>TIMEOUT</c>).
///     Expected outcomes (valid / invalid / member-state-unavailable) are values on
///     <see cref="ViesVatValidation" />, never exceptions.
/// </summary>
[PublicAPI]
public sealed class ViesApiException : Exception
{
    /// <summary>Creates a new <see cref="ViesApiException" />.</summary>
    public ViesApiException(HttpStatusCode? statusCode, string? responseBody, string message,
        Exception? innerException = null) : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    /// <summary>The HTTP status code, when the fault was an HTTP error.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>The response body (truncated), when one was read.</summary>
    public string? ResponseBody { get; }

    /// <summary>The VIES fault code (e.g. <c>SERVICE_UNAVAILABLE</c>), when the body carried one.</summary>
    public string? FaultCode { get; init; }

    /// <summary>The server-requested wait before retrying, from the <c>Retry-After</c> header, if any.</summary>
    public TimeSpan? RetryAfter { get; init; }
}