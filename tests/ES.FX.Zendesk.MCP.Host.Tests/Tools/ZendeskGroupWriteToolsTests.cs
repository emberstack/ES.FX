using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskGroupWriteToolsTests
{
    private static (ZendeskGroupWriteTools Tools, Mock<IZendeskGroupsApi> Groups) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var groups = new Mock<IZendeskGroupsApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Groups).Returns(groups.Object);
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(accessor => accessor.EffectiveMode).Returns(mode);
        return (new ZendeskGroupWriteTools(client.Object, executionMode.Object), groups);
    }

    [Fact]
    public async Task Create_Delegates()
    {
        var write = new ZendeskGroupWrite { Name = "Tier 3", IsPublic = false };
        var expected = new ZendeskGroup { Id = 5, Name = "Tier 3" };
        var (tools, groups) = Create();
        groups.Setup(api => api.CreateAsync(write, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        groups.Verify(api => api.CreateAsync(write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_Delegates()
    {
        var write = new ZendeskGroupWrite { Description = "Escalations" };
        var expected = new ZendeskGroup { Id = 5, Name = "Tier 3" };
        var (tools, groups) = Create();
        groups.Setup(api => api.UpdateAsync(5, write, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Update(5, write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        groups.Verify(api => api.UpdateAsync(5, write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_Delegates_And_Acknowledges()
    {
        var (tools, groups) = Create();
        groups.Setup(api => api.DeleteAsync(5, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await tools.Delete(5, TestContext.Current.CancellationToken);

        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete group 5", acknowledgement.Description);
        groups.Verify(api => api.DeleteAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MembershipsCreate_Delegates()
    {
        var expected = new ZendeskGroupMembership { Id = 88, UserId = 11, GroupId = 5 };
        var (tools, groups) = Create();
        groups.Setup(api => api.CreateMembershipAsync(11, 5, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.MembershipsCreate(11, 5, true, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        groups.Verify(api => api.CreateMembershipAsync(11, 5, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MembershipsCreateMany_Delegates()
    {
        var memberships = new[] { new ZendeskGroupMembership { UserId = 11, GroupId = 5 } };
        var expected = new ZendeskJobStatus { Id = "job-1" };
        var (tools, groups) = Create();
        groups.Setup(api => api.CreateManyMembershipsAsync(memberships, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.MembershipsCreateMany(memberships, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        groups.Verify(api => api.CreateManyMembershipsAsync(memberships, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MembershipsDelete_Delegates_And_Acknowledges()
    {
        var (tools, groups) = Create();
        groups.Setup(api => api.DeleteMembershipAsync(88, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await tools.MembershipsDelete(88, TestContext.Current.CancellationToken);

        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete group membership 88", acknowledgement.Description);
        groups.Verify(api => api.DeleteMembershipAsync(88, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MembershipsDeleteMany_Delegates()
    {
        var membershipIds = new long[] { 88, 89 };
        var expected = new ZendeskJobStatus { Id = "job-2" };
        var (tools, groups) = Create();
        groups.Setup(api => api.DeleteManyMembershipsAsync(membershipIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.MembershipsDeleteMany(membershipIds, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        groups.Verify(api => api.DeleteManyMembershipsAsync(membershipIds, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MembershipsMakeDefault_Delegates()
    {
        var expected = new ZendeskGroupMembershipsResult { Count = 3 };
        var (tools, groups) = Create();
        groups.Setup(api => api.MakeMembershipDefaultAsync(11, 88, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.MembershipsMakeDefault(11, 88, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        groups.Verify(api => api.MakeMembershipDefaultAsync(11, 88, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DryRun_Returns_DryRunResult_Without_Calling_Zendesk()
    {
        var write = new ZendeskGroupWrite { Name = "Tier 3" };
        var (tools, groups) = Create(McpExecutionMode.DryRun);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("create group 'Tier 3'", dryRun.Description);
        Assert.Same(write, dryRun.Request);
        groups.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadOnly_Rejects_Without_Calling_Zendesk()
    {
        var (tools, groups) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete(5, TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        groups.VerifyNoOtherCalls();
    }
}
