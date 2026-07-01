using System.Net;
using System.Text;

namespace ES.FX.Zendesk.Tests.Testing;

/// <summary>
///     An <see cref="HttpMessageHandler" /> that captures the last request and returns a canned JSON response.
/// </summary>
internal sealed class StubHttpMessageHandler(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }
}