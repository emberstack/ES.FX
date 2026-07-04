using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskFormWriteToolsTests
{
    private static (ZendeskFormWriteTools Tools, Mock<IZendeskFormsApi> Forms) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var forms = new Mock<IZendeskFormsApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Forms).Returns(forms.Object);
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(m => m.EffectiveMode).Returns(mode);
        return (new ZendeskFormWriteTools(client.Object, executionMode.Object), forms);
    }

    [Fact]
    public async Task Create_Delegates()
    {
        var write = new ZendeskTicketFormWrite { Name = "Billing" };
        var expected = new ZendeskTicketForm();
        var (tools, forms) = Create();
        forms.Setup(api => api.CreateAsync(write, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        forms.Verify(api => api.CreateAsync(write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_Delegates()
    {
        var write = new ZendeskTicketFormWrite { Active = false };
        var expected = new ZendeskTicketForm();
        var (tools, forms) = Create();
        forms.Setup(api => api.UpdateAsync(42, write, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Update(42, write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        forms.Verify(api => api.UpdateAsync(42, write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_Delegates_And_Acknowledges()
    {
        var (tools, forms) = Create();
        forms.Setup(api => api.DeleteAsync(42, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await tools.Delete(42, TestContext.Current.CancellationToken);

        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("42", acknowledgement.Description);
        forms.Verify(api => api.DeleteAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Clone_Delegates()
    {
        var expected = new ZendeskTicketForm();
        var (tools, forms) = Create();
        forms.Setup(api => api.CloneAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Clone(42, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        forms.Verify(api => api.CloneAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_DryRun_Returns_DryRunResult_Without_Calling_Client()
    {
        var write = new ZendeskTicketFormWrite { Name = "Billing" };
        var (tools, forms) = Create(McpExecutionMode.DryRun);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("create ticket form 'Billing'", dryRun.Description);
        Assert.Same(write, dryRun.Request);
        forms.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Delete_ReadOnly_Throws_Without_Calling_Client()
    {
        var (tools, forms) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete(42, TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        forms.VerifyNoOtherCalls();
    }
}
