namespace ES.FX.OpenData.Romania.Anaf.VatCheck.Tests;

internal sealed class StubHttpMessageHandler(Func<int, HttpResponseMessage> responder) : HttpMessageHandler
{
    public int CallCount { get; private set; }
    public Uri? LastRequestUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        var index = CallCount++;
        return Task.FromResult(responder(index));
    }
}