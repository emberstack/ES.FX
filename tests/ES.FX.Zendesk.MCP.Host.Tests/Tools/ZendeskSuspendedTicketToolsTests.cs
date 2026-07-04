using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Tools;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskSuspendedTicketToolsTests
{
    private static (ZendeskSuspendedTicketTools Tools, Mock<IZendeskSuspendedTicketsApi> SuspendedTickets) Create()
    {
        var suspendedTickets = new Mock<IZendeskSuspendedTicketsApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.SuspendedTickets).Returns(suspendedTickets.Object);
        return (new ZendeskSuspendedTicketTools(client.Object), suspendedTickets);
    }

    [Fact]
    public async Task List_Delegates()
    {
        var expected = new ZendeskSuspendedTicketsResult();
        var (tools, suspendedTickets) = Create();
        suspendedTickets.Setup(api => api.ListAsync(100, "cursor-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.List(100, "cursor-1", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        suspendedTickets.Verify(api => api.ListAsync(100, "cursor-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Read_Delegates_To_GetById()
    {
        var expected = new ZendeskSuspendedTicket { Id = 77 };
        var (tools, suspendedTickets) = Create();
        suspendedTickets.Setup(api => api.GetByIdAsync(77, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var suspendedTicket = await tools.Read(77, TestContext.Current.CancellationToken);

        Assert.Same(expected, suspendedTicket);
    }
}
