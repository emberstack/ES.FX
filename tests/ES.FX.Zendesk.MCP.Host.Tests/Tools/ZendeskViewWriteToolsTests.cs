using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskViewWriteToolsTests
{
    private static (ZendeskViewWriteTools Tools, Mock<IZendeskViewsApi> Views) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var views = new Mock<IZendeskViewsApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Views).Returns(views.Object);
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(m => m.EffectiveMode).Returns(mode);
        return (new ZendeskViewWriteTools(client.Object, executionMode.Object), views);
    }

    [Fact]
    public async Task Create_Delegates()
    {
        var write = new ZendeskViewWrite { Title = "Escalations" };
        var expected = new ZendeskView { Id = 7, Title = "Escalations" };
        var (tools, views) = Create();
        views.Setup(api => api.CreateAsync(write, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        views.Verify(api => api.CreateAsync(write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_Delegates()
    {
        var write = new ZendeskViewWrite { Title = "Escalations (EU)" };
        var expected = new ZendeskView { Id = 42, Title = "Escalations (EU)" };
        var (tools, views) = Create();
        views.Setup(api => api.UpdateAsync(42, write, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Update(42, write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        views.Verify(api => api.UpdateAsync(42, write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_Returns_Acknowledgement()
    {
        var (tools, views) = Create();

        var result = await tools.Delete(42, TestContext.Current.CancellationToken);

        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete view 42", acknowledgement.Description);
        views.Verify(api => api.DeleteAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DryRun_Returns_DryRunResult_Without_Calling_Client()
    {
        var write = new ZendeskViewWrite { Title = "Escalations" };
        var (tools, views) = Create(McpExecutionMode.DryRun);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("create view 'Escalations'", dryRun.Description);
        Assert.Same(write, dryRun.Request);
        views.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadOnly_Rejects_Without_Calling_Client()
    {
        var (tools, views) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete(42, TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        views.VerifyNoOtherCalls();
    }
}
