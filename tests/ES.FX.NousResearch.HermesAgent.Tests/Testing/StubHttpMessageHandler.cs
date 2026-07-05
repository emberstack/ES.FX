using System.Net;
using System.Text;

namespace ES.FX.NousResearch.HermesAgent.Tests.Testing;

/// <summary>
///     An <see cref="HttpMessageHandler" /> that captures the last request (including its body, read at send
///     time) and returns a canned response. Defaults to <c>application/json</c>; pass
///     <c>text/event-stream</c> for SSE fixtures and <paramref name="responseHeaders" /> for canned response
///     headers (e.g. the server's <c>X-Hermes-Session-Id</c> echo).
/// </summary>
internal sealed class StubHttpMessageHandler(
    string body,
    HttpStatusCode statusCode = HttpStatusCode.OK,
    string mediaType = "application/json",
    IReadOnlyDictionary<string, string>? responseHeaders = null) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    /// <summary>The last request's body as a string, captured before the request completes.</summary>
    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType)
        };
        if (responseHeaders is not null)
            foreach (var (name, value) in responseHeaders)
                response.Headers.TryAddWithoutValidation(name, value);
        return response;
    }
}