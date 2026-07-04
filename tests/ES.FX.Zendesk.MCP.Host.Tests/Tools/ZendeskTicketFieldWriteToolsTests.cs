using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskTicketFieldWriteToolsTests
{
    private static (ZendeskTicketFieldWriteTools Tools, Mock<IZendeskTicketFieldsApi> TicketFields) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var ticketFields = new Mock<IZendeskTicketFieldsApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.TicketFields).Returns(ticketFields.Object);
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(m => m.EffectiveMode).Returns(mode);
        return (new ZendeskTicketFieldWriteTools(client.Object, executionMode.Object), ticketFields);
    }

    [Fact]
    public async Task Create_Delegates()
    {
        var write = new ZendeskTicketFieldWrite { Type = "tagger", Title = "Severity" };
        var expected = new ZendeskTicketField();
        var (tools, ticketFields) = Create();
        ticketFields.Setup(api => api.CreateAsync(write, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        ticketFields.Verify(api => api.CreateAsync(write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_Delegates()
    {
        var write = new ZendeskTicketFieldWrite { Title = "Impact" };
        var expected = new ZendeskTicketField();
        var (tools, ticketFields) = Create();
        ticketFields.Setup(api => api.UpdateAsync(7, write, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Update(7, write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        ticketFields.Verify(api => api.UpdateAsync(7, write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_Delegates_And_Acknowledges()
    {
        var (tools, ticketFields) = Create();
        ticketFields.Setup(api => api.DeleteAsync(7, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await tools.Delete(7, TestContext.Current.CancellationToken);

        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("7", acknowledgement.Description);
        ticketFields.Verify(api => api.DeleteAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetOption_Delegates()
    {
        var write = new ZendeskCustomFieldOptionWrite { Name = "High", Value = "severity_high" };
        var expected = new ZendeskCustomFieldOption();
        var (tools, ticketFields) = Create();
        ticketFields.Setup(api => api.CreateOrUpdateOptionAsync(7, write, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.SetOption(7, write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        ticketFields.Verify(api => api.CreateOrUpdateOptionAsync(7, write, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteOption_Delegates_And_Acknowledges()
    {
        var (tools, ticketFields) = Create();
        ticketFields.Setup(api => api.DeleteOptionAsync(7, 99, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await tools.DeleteOption(7, 99, TestContext.Current.CancellationToken);

        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("99", acknowledgement.Description);
        ticketFields.Verify(api => api.DeleteOptionAsync(7, 99, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_DryRun_Returns_DryRunResult_Without_Calling_Client()
    {
        var write = new ZendeskTicketFieldWrite { Title = "Impact" };
        var (tools, ticketFields) = Create(McpExecutionMode.DryRun);

        var result = await tools.Update(7, write, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("update ticket field 7", dryRun.Description);
        // The echo carries the target id alongside the payload so agents can machine-inspect the plan.
        var request = Assert.IsAssignableFrom<object>(dryRun.Request);
        Assert.Equal(7L, request.GetType().GetProperty("id")!.GetValue(request));
        Assert.Same(write, request.GetType().GetProperty("field")!.GetValue(request));
        ticketFields.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Delete_ReadOnly_Throws_Without_Calling_Client()
    {
        var (tools, ticketFields) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete(7, TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        ticketFields.VerifyNoOtherCalls();
    }
}
