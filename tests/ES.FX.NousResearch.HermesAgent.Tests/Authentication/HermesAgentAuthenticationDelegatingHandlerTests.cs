using ES.FX.NousResearch.HermesAgent.Authentication;
using ES.FX.NousResearch.HermesAgent.Configuration;
using ES.FX.NousResearch.HermesAgent.Tests.Testing;
using Microsoft.Extensions.Options;

namespace ES.FX.NousResearch.HermesAgent.Tests.Authentication;

public class HermesAgentAuthenticationDelegatingHandlerTests
{
    private static HttpMessageInvoker CreateInvoker(HermesAgentClientOptions options,
        StubHttpMessageHandler inner, string optionsName = "") =>
        new(new HermesAgentAuthenticationDelegatingHandler(
            new StaticOptionsMonitor<HermesAgentClientOptions>(options), optionsName)
        {
            InnerHandler = inner
        });

    [Fact]
    public async Task Stamps_Bearer_Authorization_From_Options_On_Every_Request()
    {
        var inner = new StubHttpMessageHandler("{}");
        using var invoker = CreateInvoker(
            new HermesAgentClientOptions { ApiKey = "0123456789abcdef" }, inner);

        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:8642/v1/models");
        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("Bearer", inner.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("0123456789abcdef", inner.LastRequest?.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task Missing_ApiKey_Sends_No_Authorization_Header()
    {
        var inner = new StubHttpMessageHandler("{}");
        using var invoker = CreateInvoker(new HermesAgentClientOptions(), inner);

        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:8642/v1/health");
        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Null(inner.LastRequest?.Headers.Authorization);
    }

    [Fact]
    public async Task Whitespace_ApiKey_Sends_No_Authorization_Header()
    {
        var inner = new StubHttpMessageHandler("{}");
        using var invoker = CreateInvoker(new HermesAgentClientOptions { ApiKey = "   " }, inner);

        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:8642/v1/health");
        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Null(inner.LastRequest?.Headers.Authorization);
    }
}
