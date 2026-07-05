using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskSearchToolsTests
{
    [Fact]
    public async Task Count_Requests_SearchCount_And_Returns_Plain_Count()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"count":42}""");
        var tools = new ZendeskSearchTools(harness.CreateSupportClient());

        var count = await tools.Count("type:ticket status:open", TestContext.Current.CancellationToken);

        Assert.Equal(42L, count);
        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/search/count", request.Path);
        Assert.Contains("query=type%3Aticket%20status%3Aopen", request.Query);
    }

    [Fact]
    public async Task Count_Rejects_Blank_Query_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = new ZendeskSearchTools(harness.CreateSupportClient());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.Count(" ", TestContext.Current.CancellationToken));

        Assert.Empty(harness.Requests);
    }
}