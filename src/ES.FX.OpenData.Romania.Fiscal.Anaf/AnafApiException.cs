using System.Net;
using JetBrains.Annotations;

namespace ES.FX.OpenData.Romania.Fiscal.Anaf;

/// <summary>
///     Thrown when the ANAF API returns an infrastructure fault (non-success HTTP, an unreadable body, or a
///     non-<c>200</c> ANAF status code). A CUI that ANAF simply does not recognize is an expected outcome — it
///     appears in <see cref="AnafCompanyBatch.NotFound" /> (or a <c>null</c> from the singular lookup), not here.
/// </summary>
[PublicAPI]
public sealed class AnafApiException : Exception
{
    /// <summary>Creates a new <see cref="AnafApiException" />.</summary>
    public AnafApiException(HttpStatusCode? statusCode, string? responseBody, string message,
        Exception? innerException = null) : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    /// <summary>The HTTP status code, when the fault was an HTTP error.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>The response body (truncated), when one was read.</summary>
    public string? ResponseBody { get; }

    /// <summary>The ANAF business status code (<c>cod</c>), when ANAF returned a non-<c>200</c> code.</summary>
    public int? ErrorCode { get; init; }

    /// <summary>The server-requested wait before retrying, from the <c>Retry-After</c> header, if any.</summary>
    public TimeSpan? RetryAfter { get; init; }
}
