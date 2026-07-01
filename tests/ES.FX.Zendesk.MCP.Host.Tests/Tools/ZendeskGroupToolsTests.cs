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
        groups.Setup(api => api.ListAsync(null, 100, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.List(null, 100, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
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
        groups.Setup(api => api.GetMembershipsAsync(55, null, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.Memberships(55, null, 100, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        groups.Verify(api => api.GetMembershipsAsync(55, null, 100, It.IsAny<CancellationToken>()), Times.Once);
    }
}