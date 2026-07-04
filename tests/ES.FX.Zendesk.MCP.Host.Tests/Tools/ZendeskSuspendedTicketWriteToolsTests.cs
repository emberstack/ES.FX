using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskSuspendedTicketWriteToolsTests
{
    private static (ZendeskSuspendedTicketWriteTools Tools, Mock<IZendeskSuspendedTicketsApi> SuspendedTickets)
        Create(McpExecutionMode mode = McpExecutionMode.Default)
    {
        var suspendedTickets = new Mock<IZendeskSuspendedTicketsApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.SuspendedTickets).Returns(suspendedTickets.Object);
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(m => m.EffectiveMode).Returns(mode);
        return (new ZendeskSuspendedTicketWriteTools(client.Object, executionMode.Object), suspendedTickets);
    }

    [Fact]
    public async Task Recover_Delegates()
    {
        var expected = new ZendeskSuspendedTicketRecoveryResult();
        var (tools, suspendedTickets) = Create();
        suspendedTickets.Setup(api => api.RecoverAsync(77, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Recover(77, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        suspendedTickets.Verify(api => api.RecoverAsync(77, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecoverMany_Delegates()
    {
        var ids = new long[] { 77, 78, 79 };
        var expected = new ZendeskSuspendedTicketRecoveryResult();
        var (tools, suspendedTickets) = Create();
        suspendedTickets.Setup(api => api.RecoverManyAsync(ids, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.RecoverMany(ids, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        suspendedTickets.Verify(api => api.RecoverManyAsync(ids, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_Returns_Acknowledgement()
    {
        var (tools, suspendedTickets) = Create();

        var result = await tools.Delete(77, TestContext.Current.CancellationToken);

        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete suspended ticket 77", acknowledgement.Description);
        suspendedTickets.Verify(api => api.DeleteAsync(77, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteMany_Returns_Acknowledgement()
    {
        var ids = new long[] { 77, 78 };
        var (tools, suspendedTickets) = Create();

        var result = await tools.DeleteMany(ids, TestContext.Current.CancellationToken);

        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete 2 suspended tickets", acknowledgement.Description);
        suspendedTickets.Verify(api => api.DeleteManyAsync(ids, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DryRun_Returns_DryRunResult_Without_Calling_Client()
    {
        var ids = new long[] { 77, 78, 79 };
        var (tools, suspendedTickets) = Create(McpExecutionMode.DryRun);

        var result = await tools.RecoverMany(ids, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("recover 3 suspended tickets", dryRun.Description);
        Assert.NotNull(dryRun.Request);
        suspendedTickets.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadOnly_Rejects_Without_Calling_Client()
    {
        var (tools, suspendedTickets) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.DeleteMany([77, 78], TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        suspendedTickets.VerifyNoOtherCalls();
    }
}
