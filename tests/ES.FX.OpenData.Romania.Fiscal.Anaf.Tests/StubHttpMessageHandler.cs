namespace ES.FX.OpenData.Romania.Fiscal.Anaf.Tests;

internal sealed class StubHttpMessageHandler(Func<int, HttpResponseMessage> responder) : HttpMessageHandler
{
    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var index = CallCount++;
        return Task.FromResult(responder(index));
    }
}
