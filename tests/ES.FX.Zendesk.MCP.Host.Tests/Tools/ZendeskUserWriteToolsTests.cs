using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskUserWriteToolsTests
{
    private static (ZendeskUserWriteTools Tools, Mock<IZendeskUsersApi> Users) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var users = new Mock<IZendeskUsersApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Users).Returns(users.Object);
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(m => m.EffectiveMode).Returns(mode);
        return (new ZendeskUserWriteTools(client.Object, executionMode.Object), users);
    }

    [Fact]
    public async Task Create_Delegates()
    {
        var expected = new ZendeskUser { Id = 7, Name = "Jane" };
        var write = new ZendeskUserWrite { Name = "Jane", Email = "jane@example.com" };
        var (tools, users) = Create();
        users.Setup(api => api.CreateAsync(write, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.CreateAsync(write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrUpdate_Delegates()
    {
        var expected = new ZendeskUser { Id = 8 };
        var write = new ZendeskUserWrite { Email = "jane@example.com" };
        var (tools, users) = Create();
        users.Setup(api => api.CreateOrUpdateAsync(write, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.CreateOrUpdate(write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.CreateOrUpdateAsync(write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateMany_Delegates()
    {
        var expected = new ZendeskJobStatus { Id = "job-1" };
        var writes = new[] { new ZendeskUserWrite { Name = "A" }, new ZendeskUserWrite { Name = "B" } };
        var (tools, users) = Create();
        users.Setup(api => api.CreateManyAsync(writes, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.CreateMany(writes, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.CreateManyAsync(writes, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrUpdateMany_Delegates()
    {
        var expected = new ZendeskJobStatus { Id = "job-2" };
        var writes = new[] { new ZendeskUserWrite { Email = "a@example.com" } };
        var (tools, users) = Create();
        users.Setup(api => api.CreateOrUpdateManyAsync(writes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.CreateOrUpdateMany(writes, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.CreateOrUpdateManyAsync(writes, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_Delegates()
    {
        var expected = new ZendeskUser { Id = 42 };
        var write = new ZendeskUserWrite { Notes = "VIP" };
        var (tools, users) = Create();
        users.Setup(api => api.UpdateAsync(42, write, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Update(42, write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.UpdateAsync(42, write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateMany_Delegates()
    {
        var expected = new ZendeskJobStatus { Id = "job-3" };
        var ids = new long[] { 1, 2, 3 };
        var change = new ZendeskUserWrite { Suspended = true };
        var (tools, users) = Create();
        users.Setup(api => api.UpdateManyAsync(ids, change, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.UpdateMany(ids, change, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.UpdateManyAsync(ids, change, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateManyBatch_Delegates()
    {
        var expected = new ZendeskJobStatus { Id = "job-4" };
        var writes = new[]
        {
            new ZendeskUserWrite { Id = 1, Notes = "first" },
            new ZendeskUserWrite { Id = 2, Notes = "second" }
        };
        var (tools, users) = Create();
        users.Setup(api =>
                api.UpdateManyAsync((IReadOnlyList<ZendeskUserWrite>)writes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.UpdateManyBatch(writes, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(
            api => api.UpdateManyAsync((IReadOnlyList<ZendeskUserWrite>)writes, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Merge_Delegates_With_Loser_And_Winner_In_Order()
    {
        var expected = new ZendeskUser { Id = 9 };
        var (tools, users) = Create();
        users.Setup(api => api.MergeAsync(5, 9, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Merge(5, 9, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.MergeAsync(5, 9, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_Delegates()
    {
        var expected = new ZendeskUser { Id = 12 };
        var (tools, users) = Create();
        users.Setup(api => api.DeleteAsync(12, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Delete(12, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.DeleteAsync(12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteMany_Delegates()
    {
        var expected = new ZendeskJobStatus { Id = "job-5" };
        var ids = new long[] { 4, 5 };
        var (tools, users) = Create();
        users.Setup(api => api.DeleteManyAsync(ids, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.DeleteMany(ids, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.DeleteManyAsync(ids, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePermanently_Delegates()
    {
        var expected = new ZendeskUser { Id = 33 };
        var (tools, users) = Create();
        users.Setup(api => api.DeletePermanentlyAsync(33, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.DeletePermanently(33, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.DeletePermanentlyAsync(33, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IdentitiesCreate_Delegates()
    {
        var expected = new ZendeskUserIdentity { Id = 100 };
        var write = new ZendeskUserIdentityWrite { Type = "email", Value = "new@example.com" };
        var (tools, users) = Create();
        users.Setup(api => api.CreateIdentityAsync(42, write, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.IdentitiesCreate(42, write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.CreateIdentityAsync(42, write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IdentitiesUpdate_Delegates()
    {
        var expected = new ZendeskUserIdentity { Id = 100 };
        var write = new ZendeskUserIdentityWrite { Verified = true };
        var (tools, users) = Create();
        users.Setup(api => api.UpdateIdentityAsync(42, 100, write, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.IdentitiesUpdate(42, 100, write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.UpdateIdentityAsync(42, 100, write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IdentitiesMakePrimary_Delegates()
    {
        var expected = new ZendeskUserIdentitiesResult { Count = 2 };
        var (tools, users) = Create();
        users.Setup(api => api.MakeIdentityPrimaryAsync(42, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.IdentitiesMakePrimary(42, 100, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.MakeIdentityPrimaryAsync(42, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IdentitiesVerify_Delegates()
    {
        var expected = new ZendeskUserIdentity { Id = 100, Verified = true };
        var (tools, users) = Create();
        users.Setup(api => api.VerifyIdentityAsync(42, 100, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.IdentitiesVerify(42, 100, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        users.Verify(api => api.VerifyIdentityAsync(42, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IdentitiesRequestVerification_Delegates_And_Acknowledges()
    {
        var (tools, users) = Create();
        users.Setup(api => api.RequestIdentityVerificationAsync(42, 100, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await tools.IdentitiesRequestVerification(42, 100, TestContext.Current.CancellationToken);

        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("identity 100", acknowledgement.Description);
        users.Verify(api => api.RequestIdentityVerificationAsync(42, 100, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IdentitiesDelete_Delegates_And_Acknowledges()
    {
        var (tools, users) = Create();
        users.Setup(api => api.DeleteIdentityAsync(42, 100, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await tools.IdentitiesDelete(42, 100, TestContext.Current.CancellationToken);

        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("identity 100", acknowledgement.Description);
        users.Verify(api => api.DeleteIdentityAsync(42, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DryRun_Returns_DryRunResult_And_Does_Not_Call_Client()
    {
        var write = new ZendeskUserWrite { Name = "Jane", Email = "jane@example.com" };
        var (tools, users) = Create(McpExecutionMode.DryRun);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("create user 'Jane'", dryRun.Description);
        Assert.Same(write, dryRun.Request);
        users.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadOnly_Throws_And_Does_Not_Call_Client()
    {
        var (tools, users) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete(12, TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        users.VerifyNoOtherCalls();
    }
}
