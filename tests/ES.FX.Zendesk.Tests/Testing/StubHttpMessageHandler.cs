using System.Net;
using System.Text;

namespace ES.FX.Zendesk.Tests.Testing;

/// <summary>
///     An <see cref="HttpMessageHandler" /> that captures the last request (including its body, read at send
///     time) and returns a canned JSON response.
/// </summary>
internal sealed class StubHttpMessageHandler(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    : HttpMessageHandler
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
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}