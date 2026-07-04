using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskCustomStatusWriteToolsTests
{
    private static (ZendeskCustomStatusWriteTools Tools, Mock<IZendeskCustomStatusesApi> CustomStatuses) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var customStatuses = new Mock<IZendeskCustomStatusesApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.CustomStatuses).Returns(customStatuses.Object);
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(m => m.EffectiveMode).Returns(mode);
        return (new ZendeskCustomStatusWriteTools(client.Object, executionMode.Object), customStatuses);
    }

    [Fact]
    public async Task Create_Delegates()
    {
        var write = new ZendeskCustomStatusWrite { StatusCategory = "hold", AgentLabel = "Awaiting vendor" };
        var expected = new ZendeskCustomStatus { Id = 11 };
        var (tools, customStatuses) = Create();
        customStatuses.Setup(api => api.CreateAsync(write, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        customStatuses.Verify(api => api.CreateAsync(write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_Delegates()
    {
        var write = new ZendeskCustomStatusWrite { AgentLabel = "Awaiting supplier", Active = false };
        var expected = new ZendeskCustomStatus { Id = 11 };
        var (tools, customStatuses) = Create();
        customStatuses.Setup(api => api.UpdateAsync(11, write, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.Update(11, write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        customStatuses.Verify(api => api.UpdateAsync(11, write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_Returns_Acknowledgement()
    {
        var (tools, customStatuses) = Create();

        var result = await tools.Delete(11, TestContext.Current.CancellationToken);

        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete custom ticket status 11", acknowledgement.Description);
        customStatuses.Verify(api => api.DeleteAsync(11, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DryRun_Returns_DryRunResult_Without_Calling_Client()
    {
        var write = new ZendeskCustomStatusWrite { StatusCategory = "hold", AgentLabel = "Awaiting vendor" };
        var (tools, customStatuses) = Create(McpExecutionMode.DryRun);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("create custom ticket status 'Awaiting vendor'", dryRun.Description);
        Assert.Same(write, dryRun.Request);
        customStatuses.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadOnly_Rejects_Without_Calling_Client()
    {
        var (tools, customStatuses) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Update(11, new ZendeskCustomStatusWrite { Active = false },
                TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        customStatuses.VerifyNoOtherCalls();
    }
}
