using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Tools;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskViewToolsTests
{
    private static (ZendeskViewTools Tools, Mock<IZendeskViewsApi> Views) Create()
    {
        var views = new Mock<IZendeskViewsApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Views).Returns(views.Object);
        return (new ZendeskViewTools(client.Object), views);
    }

    [Fact]
    public async Task List_Delegates()
    {
        var expected = new ZendeskViewsResult();
        var (tools, views) = Create();
        views.Setup(api => api.ListAsync(true, 50, "cursor-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.List(true, 50, "cursor-1", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        views.Verify(api => api.ListAsync(true, 50, "cursor-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Read_Delegates_To_GetById()
    {
        var expected = new ZendeskView { Id = 12, Title = "Open tickets" };
        var (tools, views) = Create();
        views.Setup(api => api.GetByIdAsync(12, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var view = await tools.Read(12, TestContext.Current.CancellationToken);

        Assert.Same(expected, view);
    }

    [Fact]
    public async Task Tickets_Delegates()
    {
        var expected = new ZendeskTicketsResult { Count = 7 };
        var include = new[] { "users", "groups" };
        var (tools, views) = Create();
        views.Setup(api => api.GetTicketsAsync(12, 2, 25, include, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.Tickets(12, 2, 25, include, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        views.Verify(api => api.GetTicketsAsync(12, 2, 25, include, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Count_Delegates_To_GetTicketCount()
    {
        var expected = new ZendeskViewCount();
        var (tools, views) = Create();
        views.Setup(api => api.GetTicketCountAsync(12, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var count = await tools.Count(12, TestContext.Current.CancellationToken);

        Assert.Same(expected, count);
    }
}
