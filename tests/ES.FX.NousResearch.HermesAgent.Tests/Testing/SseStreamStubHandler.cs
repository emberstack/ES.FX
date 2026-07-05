using System.Net;
using System.Net.Http.Headers;

namespace ES.FX.NousResearch.HermesAgent.Tests.Testing;

/// <summary>
///     A stub handler that returns the supplied stream as a <c>text/event-stream</c> response body, so SSE
///     tests can control delivery read-by-read (blocking reads for cancellation tests, disposal tracking for
///     response-lifetime tests) — a buffered <see cref="StringContent" /> cannot observe either.
/// </summary>
internal sealed class SseStreamStubHandler(Stream stream) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}
