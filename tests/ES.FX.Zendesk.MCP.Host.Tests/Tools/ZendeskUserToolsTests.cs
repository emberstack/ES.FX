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
        users.Setup(api => api.GetManyAsync(It.IsAny<IReadOnlyList<long>>(), It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.ReadMany([1, 2], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.GetManyAsync(
            It.Is<IReadOnlyList<long>>(ids => ids.Count == 2 && ids.Contains(1) && ids.Contains(2)),
            It.IsAny<IReadOnlyList<string>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReadMany_Passes_Include_Through()
    {
        var expected = new ZendeskUsersResult { Count = 1 };
        var include = new[] { "organizations" };
        var (tools, users) = Create();
        users.Setup(api => api.GetManyAsync(It.IsAny<IReadOnlyList<long>>(), include,
            It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.ReadMany([1], include, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.GetManyAsync(It.IsAny<IReadOnlyList<long>>(), include,
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

    [Fact]
    public async Task List_Delegates_With_Role_And_Cursor()
    {
        var expected = new ZendeskUsersResult { Count = 9 };
        var (tools, users) = Create();
        users.Setup(api => api.ListAsync("agent", 50, "cursor-1", It.IsAny<IReadOnlyList<string>?>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.List("agent", 50, "cursor-1", ["groups"], TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.ListAsync("agent", 50, "cursor-1",
            It.Is<IReadOnlyList<string>?>(i => i != null && i.Count == 1 && i[0] == "groups"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Count_Delegates_With_Role()
    {
        var expected = new ZendeskCount { Value = 123 };
        var (tools, users) = Create();
        users.Setup(api => api.CountAsync("end-user", It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Count("end-user", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.CountAsync("end-user", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Autocomplete_Delegates()
    {
        var expected = new ZendeskUsersResult { Count = 2 };
        var (tools, users) = Create();
        users.Setup(api => api.AutocompleteAsync("ja", 2, 25, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Autocomplete("ja", 2, 25, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.AutocompleteAsync("ja", 2, 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Related_Delegates()
    {
        var expected = new ZendeskUserRelated();
        var (tools, users) = Create();
        users.Setup(api => api.GetRelatedInformationAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Related(42, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.GetRelatedInformationAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Identities_Delegates_With_Cursor()
    {
        var expected = new ZendeskUserIdentitiesResult { Count = 3 };
        var (tools, users) = Create();
        users.Setup(api => api.GetIdentitiesAsync(42, 10, "cursor-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.Identities(42, 10, "cursor-2", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.GetIdentitiesAsync(42, 10, "cursor-2", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Groups_Delegates()
    {
        var expected = new ZendeskGroupsResult { Count = 2 };
        var (tools, users) = Create();
        users.Setup(api => api.GetGroupsAsync(42, 1, 100, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Groups(42, 1, 100, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.GetGroupsAsync(42, 1, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Organizations_Delegates()
    {
        var expected = new ZendeskOrganizationsResult { Count = 1 };
        var (tools, users) = Create();
        users.Setup(api => api.GetOrganizationsAsync(42, null, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.Organizations(42, null, 100, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.GetOrganizationsAsync(42, null, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignedTickets_Delegates()
    {
        var expected = new ZendeskTicketsResult { Count = 5 };
        var (tools, users) = Create();
        users.Setup(api => api.GetAssignedTicketsAsync(42, null, 25, It.IsAny<IReadOnlyList<string>?>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.AssignedTickets(42, null, 25, null, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.GetAssignedTicketsAsync(42, null, 25, It.IsAny<IReadOnlyList<string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CcdTickets_Delegates()
    {
        var expected = new ZendeskTicketsResult { Count = 6 };
        var (tools, users) = Create();
        users.Setup(api => api.GetCcdTicketsAsync(42, 2, 50, It.IsAny<IReadOnlyList<string>?>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.CcdTickets(42, 2, 50, ["users"], TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.GetCcdTicketsAsync(42, 2, 50,
            It.Is<IReadOnlyList<string>?>(i => i != null && i.Count == 1 && i[0] == "users"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Tags_Delegates()
    {
        var expected = new ZendeskTagNamesResult { Tags = ["vip"] };
        var (tools, users) = Create();
        users.Setup(api => api.GetTagsAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Tags(42, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.GetTagsAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }
}