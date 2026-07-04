using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskMacroWriteToolsTests
{
    private static (ZendeskMacroWriteTools Tools, Mock<IZendeskMacrosApi> Macros) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var macros = new Mock<IZendeskMacrosApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Macros).Returns(macros.Object);
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(m => m.EffectiveMode).Returns(mode);
        return (new ZendeskMacroWriteTools(client.Object, executionMode.Object), macros);
    }

    [Fact]
    public async Task Create_Delegates()
    {
        var write = new ZendeskMacroWrite
        {
            Title = "Close and thank",
            Actions = [new ZendeskMacroActionWrite { Field = "status", Value = "solved" }]
        };
        var expected = new ZendeskMacro();
        var (tools, macros) = Create();
        macros.Setup(api => api.CreateAsync(write, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        macros.Verify(api => api.CreateAsync(write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_Delegates()
    {
        var write = new ZendeskMacroWrite { Active = false };
        var expected = new ZendeskMacro();
        var (tools, macros) = Create();
        macros.Setup(api => api.UpdateAsync(31, write, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Update(31, write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        macros.Verify(api => api.UpdateAsync(31, write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_Delegates_And_Acknowledges()
    {
        var (tools, macros) = Create();
        macros.Setup(api => api.DeleteAsync(31, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await tools.Delete(31, TestContext.Current.CancellationToken);

        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("31", acknowledgement.Description);
        macros.Verify(api => api.DeleteAsync(31, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_DryRun_Returns_DryRunResult_Without_Calling_Client()
    {
        var write = new ZendeskMacroWrite { Title = "Close and thank" };
        var (tools, macros) = Create(McpExecutionMode.DryRun);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("create macro 'Close and thank'", dryRun.Description);
        Assert.Same(write, dryRun.Request);
        macros.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Delete_ReadOnly_Throws_Without_Calling_Client()
    {
        var (tools, macros) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete(31, TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        macros.VerifyNoOtherCalls();
    }
}
