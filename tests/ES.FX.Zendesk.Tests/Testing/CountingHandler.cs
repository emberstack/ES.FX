namespace ES.FX.Zendesk.Tests.Testing;

/// <summary>
///     An <see cref="HttpMessageHandler" /> that counts calls (thread-safe) and returns a caller-supplied response.
/// </summary>
internal sealed class CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult(responder(request));
    }
}

/// <summary>
///     A minimal <see cref="IHttpClientFactory" /> that hands out clients over a single handler.
/// </summary>
internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, false);
}