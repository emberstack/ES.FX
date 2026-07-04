using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Tools;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskGroupToolsTests
{
    private static (ZendeskGroupTools Tools, Mock<IZendeskGroupsApi> Groups) Create()
    {
        var groups = new Mock<IZendeskGroupsApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Groups).Returns(groups.Object);
        return (new ZendeskGroupTools(client.Object), groups);
    }

    [Fact]
    public async Task List_Delegates()
    {
        var expected = new ZendeskGroupsResult { Count = 3 };
        var (tools, groups) = Create();
        groups.Setup(api => api.ListAsync(null, 100, It.IsAny<IReadOnlyList<string>?>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.List(null, 100, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task List_Passes_Include_Through()
    {
        var expected = new ZendeskGroupsResult { Count = 1 };
        var include = new[] { "users" };
        var (tools, groups) = Create();
        groups.Setup(api => api.ListAsync(null, 100, include, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.List(null, 100, include, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        groups.Verify(api => api.ListAsync(null, 100, include, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Read_Delegates_To_GetById()
    {
        var expected = new ZendeskGroup { Id = 55, Name = "Tier 2" };
        var (tools, groups) = Create();
        groups.Setup(api => api.GetByIdAsync(55, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var group = await tools.Read(55, TestContext.Current.CancellationToken);

        Assert.Same(expected, group);
    }

    [Fact]
    public async Task Memberships_Delegates()
    {
        var expected = new ZendeskGroupMembershipsResult { Count = 4 };
        var (tools, groups) = Create();
        groups.Setup(api => api.GetMembershipsAsync(55, null, 100, It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.Memberships(55, null, 100, null, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        groups.Verify(api => api.GetMembershipsAsync(55, null, 100, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Memberships_Passes_Include_Through()
    {
        var include = new[] { "users" };
        var expected = new ZendeskGroupMembershipsResult { Count = 2 };
        var (tools, groups) = Create();
        groups.Setup(api => api.GetMembershipsAsync(55, null, 100,
                It.Is<IReadOnlyList<string>?>(value => ReferenceEquals(value, include)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.Memberships(55, null, 100, include, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        groups.Verify(api => api.GetMembershipsAsync(55, null, 100,
            It.Is<IReadOnlyList<string>?>(value => ReferenceEquals(value, include)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Assignable_Delegates()
    {
        var expected = new ZendeskGroupsResult { Count = 2 };
        var (tools, groups) = Create();
        groups.Setup(api => api.GetAssignableAsync(2, 50, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Assignable(2, 50, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        groups.Verify(api => api.GetAssignableAsync(2, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Count_Delegates()
    {
        var expected = new ZendeskCount { Value = 12 };
        var (tools, groups) = Create();
        groups.Setup(api => api.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Count(TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task Users_Delegates()
    {
        var expected = new ZendeskUsersResult { Count = 6 };
        var (tools, groups) = Create();
        groups.Setup(api => api.GetUsersAsync(55, 1, 30, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Users(55, 1, 30, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        groups.Verify(api => api.GetUsersAsync(55, 1, 30, It.IsAny<CancellationToken>()), Times.Once);
    }
}