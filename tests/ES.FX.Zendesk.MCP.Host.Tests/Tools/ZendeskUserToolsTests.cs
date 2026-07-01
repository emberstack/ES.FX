using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Tools;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskUserToolsTests
{
    private static (ZendeskUserTools Tools, Mock<IZendeskUsersApi> Users) Create()
    {
        var users = new Mock<IZendeskUsersApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Users).Returns(users.Object);
        return (new ZendeskUserTools(client.Object), users);
    }

    [Fact]
    public async Task Whoami_Returns_Current_User_From_Client()
    {
        var expected = new ZendeskUser { Id = 7, Name = "Agent Smith" };
        var (tools, users) = Create();
        users.Setup(api => api.GetCurrentUserAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var user = await tools.Whoami(TestContext.Current.CancellationToken);

        Assert.Same(expected, user);
    }

    [Fact]
    public async Task Read_Delegates_To_GetById()
    {
        var expected = new ZendeskUser { Id = 42 };
        var (tools, users) = Create();
        users.Setup(api => api.GetByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var user = await tools.Read(42, TestContext.Current.CancellationToken);

        Assert.Same(expected, user);
    }

    [Fact]
    public async Task Search_Passes_Parameters_Through()
    {
        var expected = new ZendeskUsersResult { Count = 3 };
        var (tools, users) = Create();
        users.Setup(api => api.SearchAsync("role:agent", 2, 50, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Search("role:agent", 2, 50, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task ReadMany_Delegates_To_GetMany()
    {
        var expected = new ZendeskUsersResult { Count = 2 };
        var (tools, users) = Create();
        users.Setup(api => api.GetManyAsync(It.IsAny<IReadOnlyList<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.ReadMany([1, 2], TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.GetManyAsync(
            It.Is<IReadOnlyList<long>>(ids => ids.Count == 2 && ids.Contains(1) && ids.Contains(2)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestedTickets_Delegates()
    {
        var expected = new ZendeskTicketsResult { Count = 4 };
        var (tools, users) = Create();
        users.Setup(api => api.GetRequestedTicketsAsync(42, null, 25, It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.RequestedTickets(42, null, 25, null, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.GetRequestedTicketsAsync(42, null, 25, It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}