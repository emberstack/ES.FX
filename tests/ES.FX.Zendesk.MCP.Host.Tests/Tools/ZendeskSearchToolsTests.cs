using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Tools;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskSearchToolsTests
{
    private static (ZendeskSearchTools Tools, Mock<IZendeskSearchApi> Search) Create()
    {
        var search = new Mock<IZendeskSearchApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Search).Returns(search.Object);
        return (new ZendeskSearchTools(client.Object), search);
    }

    [Fact]
    public async Task Count_Delegates()
    {
        var (tools, search) = Create();
        search.Setup(api => api.CountAsync("type:ticket status:open", It.IsAny<CancellationToken>()))
            .ReturnsAsync(42L);

        var count = await tools.Count("type:ticket status:open", TestContext.Current.CancellationToken);

        Assert.Equal(42L, count);
        search.Verify(api => api.CountAsync("type:ticket status:open", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExportTickets_Delegates()
    {
        var expected = new ZendeskTicketSearchExportResults();
        var (tools, search) = Create();
        search.Setup(api => api.ExportTicketsAsync("status:open", 100, "cursor-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.ExportTickets("status:open", 100, "cursor-1",
            TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        search.Verify(api => api.ExportTicketsAsync("status:open", 100, "cursor-1",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
