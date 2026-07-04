using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskOrganizationWriteToolsTests
{
    private static (ZendeskOrganizationWriteTools Tools, Mock<IZendeskOrganizationsApi> Organizations) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var organizations = new Mock<IZendeskOrganizationsApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Organizations).Returns(organizations.Object);
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(accessor => accessor.EffectiveMode).Returns(mode);
        return (new ZendeskOrganizationWriteTools(client.Object, executionMode.Object), organizations);
    }

    [Fact]
    public async Task Create_Delegates()
    {
        var write = new ZendeskOrganizationWrite { Name = "Acme" };
        var expected = new ZendeskOrganization { Id = 7, Name = "Acme" };
        var (tools, organizations) = Create();
        organizations.Setup(api => api.CreateAsync(write, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        organizations.Verify(api => api.CreateAsync(write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateMany_Delegates()
    {
        var writes = new[] { new ZendeskOrganizationWrite { Name = "A" }, new ZendeskOrganizationWrite { Name = "B" } };
        var expected = new ZendeskJobStatus { Id = "job-1" };
        var (tools, organizations) = Create();
        organizations.Setup(api => api.CreateManyAsync(writes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.CreateMany(writes, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        organizations.Verify(api => api.CreateManyAsync(writes, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrUpdate_Delegates()
    {
        var write = new ZendeskOrganizationWrite { ExternalId = "ext-9", Name = "Acme" };
        var expected = new ZendeskOrganization { Id = 9, Name = "Acme" };
        var (tools, organizations) = Create();
        organizations.Setup(api => api.CreateOrUpdateAsync(write, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.CreateOrUpdate(write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        organizations.Verify(api => api.CreateOrUpdateAsync(write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_Delegates()
    {
        var write = new ZendeskOrganizationWrite { Notes = "vip" };
        var expected = new ZendeskOrganization { Id = 42, Name = "Acme" };
        var (tools, organizations) = Create();
        organizations.Setup(api => api.UpdateAsync(42, write, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.Update(42, write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        organizations.Verify(api => api.UpdateAsync(42, write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateMany_Delegates()
    {
        var ids = new long[] { 1, 2, 3 };
        var change = new ZendeskOrganizationWrite { Tags = ["vip"] };
        var expected = new ZendeskJobStatus { Id = "job-2" };
        var (tools, organizations) = Create();
        organizations.Setup(api => api.UpdateManyAsync(ids, change, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.UpdateMany(ids, change, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        organizations.Verify(api => api.UpdateManyAsync(ids, change, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateManyBatch_Delegates()
    {
        var writes = new[] { new ZendeskOrganizationWrite { Id = 1, Notes = "a" } };
        var expected = new ZendeskJobStatus { Id = "job-3" };
        var (tools, organizations) = Create();
        organizations.Setup(api => api.UpdateManyAsync(writes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.UpdateManyBatch(writes, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        organizations.Verify(api => api.UpdateManyAsync(writes, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_Delegates_And_Acknowledges()
    {
        var (tools, organizations) = Create();
        organizations.Setup(api => api.DeleteAsync(42, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await tools.Delete(42, TestContext.Current.CancellationToken);

        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete organization 42", acknowledgement.Description);
        organizations.Verify(api => api.DeleteAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteMany_Delegates()
    {
        var ids = new long[] { 4, 5 };
        var expected = new ZendeskJobStatus { Id = "job-4" };
        var (tools, organizations) = Create();
        organizations.Setup(api => api.DeleteManyAsync(ids, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.DeleteMany(ids, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        organizations.Verify(api => api.DeleteManyAsync(ids, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Merge_Delegates()
    {
        var expected = new ZendeskOrganizationMerge { Id = "merge-1", LoserId = 5, WinnerId = 9 };
        var (tools, organizations) = Create();
        organizations.Setup(api => api.MergeAsync(5, 9, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Merge(5, 9, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        organizations.Verify(api => api.MergeAsync(5, 9, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MembershipsCreate_Delegates()
    {
        var expected = new ZendeskOrganizationMembership { Id = 77, UserId = 11, OrganizationId = 22 };
        var (tools, organizations) = Create();
        organizations.Setup(api => api.CreateMembershipAsync(11, 22, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.MembershipsCreate(11, 22, true, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        organizations.Verify(api => api.CreateMembershipAsync(11, 22, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MembershipsCreateMany_Delegates()
    {
        var memberships = new[] { new ZendeskOrganizationMembership { UserId = 11, OrganizationId = 22 } };
        var expected = new ZendeskJobStatus { Id = "job-5" };
        var (tools, organizations) = Create();
        organizations.Setup(api => api.CreateManyMembershipsAsync(memberships, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.MembershipsCreateMany(memberships, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        organizations.Verify(api => api.CreateManyMembershipsAsync(memberships, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MembershipsDelete_Delegates_And_Acknowledges()
    {
        var (tools, organizations) = Create();
        organizations.Setup(api => api.DeleteMembershipAsync(77, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await tools.MembershipsDelete(77, TestContext.Current.CancellationToken);

        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete organization membership 77", acknowledgement.Description);
        organizations.Verify(api => api.DeleteMembershipAsync(77, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MembershipsDeleteMany_Delegates()
    {
        var membershipIds = new long[] { 77, 78 };
        var expected = new ZendeskJobStatus { Id = "job-6" };
        var (tools, organizations) = Create();
        organizations.Setup(api => api.DeleteManyMembershipsAsync(membershipIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.MembershipsDeleteMany(membershipIds, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        organizations.Verify(api => api.DeleteManyMembershipsAsync(membershipIds, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MembershipsMakeDefault_Delegates()
    {
        var expected = new ZendeskOrganizationMembershipsResult { Count = 2 };
        var (tools, organizations) = Create();
        organizations.Setup(api => api.MakeMembershipDefaultAsync(11, 77, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.MembershipsMakeDefault(11, 77, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        organizations.Verify(api => api.MakeMembershipDefaultAsync(11, 77, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DryRun_Returns_DryRunResult_Without_Calling_Zendesk()
    {
        var write = new ZendeskOrganizationWrite { Name = "Acme" };
        var (tools, organizations) = Create(McpExecutionMode.DryRun);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("create organization 'Acme'", dryRun.Description);
        Assert.Same(write, dryRun.Request);
        organizations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadOnly_Rejects_Without_Calling_Zendesk()
    {
        var (tools, organizations) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete(42, TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        organizations.VerifyNoOtherCalls();
    }
}
