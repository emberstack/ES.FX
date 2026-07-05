using System.Net;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskToolInvokerTests
{
    private static Mock<IMcpExecutionModeAccessor> Mode(McpExecutionMode mode)
    {
        var accessor = new Mock<IMcpExecutionModeAccessor>();
        accessor.SetupGet(a => a.EffectiveMode).Returns(mode);
        return accessor;
    }

    [Fact]
    public async Task InvokeAsync_Returns_Operation_Result()
    {
        var result = await ZendeskToolInvoker.InvokeAsync(() => Task.FromResult(42));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task InvokeAsync_Translates_ZendeskApiException_With_Body()
    {
        var exception = await Assert.ThrowsAsync<McpException>(() => ZendeskToolInvoker.InvokeAsync<int>(
            () => throw new ZendeskApiException(HttpStatusCode.UnprocessableEntity, "{\"error\":\"nope\"}",
                "boom")));

        Assert.Contains("422", exception.Message);
        Assert.Contains("UnprocessableEntity", exception.Message);
        Assert.Contains("{\"error\":\"nope\"}", exception.Message);
    }

    [Fact]
    public async Task InvokeAsync_Translates_ZendeskApiException_With_RetryAfter_Hint()
    {
        var exception = await Assert.ThrowsAsync<McpException>(() => ZendeskToolInvoker.InvokeAsync<int>(
            () => throw new ZendeskApiException(HttpStatusCode.TooManyRequests,
                "{\"error\":\"RateLimited\"}", "boom") { RetryAfter = TimeSpan.FromSeconds(90) }));

        Assert.Contains("429", exception.Message);
        Assert.Contains("TooManyRequests", exception.Message);
        Assert.Contains("{\"error\":\"RateLimited\"}", exception.Message);
        Assert.Contains("Retry after 90 seconds.", exception.Message);
    }

    [Fact]
    public async Task InvokeAsync_Translates_ZendeskApiException_Without_RetryAfter_Hint()
    {
        var exception = await Assert.ThrowsAsync<McpException>(() => ZendeskToolInvoker.InvokeAsync<int>(
            () => throw new ZendeskApiException(HttpStatusCode.TooManyRequests,
                "{\"error\":\"RateLimited\"}", "boom")));

        Assert.Contains("429", exception.Message);
        Assert.Contains("{\"error\":\"RateLimited\"}", exception.Message);
        Assert.DoesNotContain("Retry after", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvokeAsync_Translates_ZendeskApiException_Without_Body(string? body)
    {
        var exception = await Assert.ThrowsAsync<McpException>(() => ZendeskToolInvoker.InvokeAsync<int>(
            () => throw new ZendeskApiException(HttpStatusCode.NotFound, body, "boom")));

        Assert.Contains("404", exception.Message);
        Assert.DoesNotContain("Zendesk response:", exception.Message);
    }

    [Fact]
    public async Task InvokeAsync_Passes_Other_Exceptions_Through_Untranslated()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => ZendeskToolInvoker.InvokeAsync<int>(
            () => throw new InvalidOperationException("untouched")));
    }

    [Fact]
    public async Task InvokeWriteAsync_Executes_In_Default_Mode()
    {
        var result = await ZendeskToolInvoker.InvokeWriteAsync(Mode(McpExecutionMode.Default).Object,
            "do the thing", () => Task.FromResult("payload"), new { id = 1 });

        Assert.Equal("payload", result);
    }

    [Fact]
    public async Task InvokeWriteAsync_Bare_Task_Returns_Acknowledgement_In_Default_Mode()
    {
        var executed = false;

        var result = await ZendeskToolInvoker.InvokeWriteAsync(Mode(McpExecutionMode.Default).Object,
            "restore ticket 5", () =>
            {
                executed = true;
                return Task.CompletedTask;
            });

        Assert.True(executed);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Equal("completed", acknowledgement.Status);
        Assert.Contains("restore ticket 5", acknowledgement.Description);
    }

    [Fact]
    public async Task InvokeWriteAsync_DryRun_Skips_Operation_And_Reports()
    {
        var executed = false;
        var request = new { subject = "hello" };

        var result = await ZendeskToolInvoker.InvokeWriteAsync(Mode(McpExecutionMode.DryRun).Object,
            "create a ticket", () =>
            {
                executed = true;
                return Task.FromResult("payload");
            }, request);

        Assert.False(executed);
        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.Equal("dry_run", dryRun.Status);
        Assert.False(dryRun.Executed);
        Assert.Contains("create a ticket", dryRun.Description);
        Assert.Same(request, dryRun.Request);
    }

    [Fact]
    public async Task InvokeWriteAsync_ReadOnly_Rejects_Without_Executing()
    {
        var executed = false;

        var exception = await Assert.ThrowsAsync<McpException>(() => ZendeskToolInvoker.InvokeWriteAsync(
            Mode(McpExecutionMode.ReadOnly).Object, "delete ticket 5", () =>
            {
                executed = true;
                return Task.FromResult(1);
            }));

        Assert.False(executed);
        Assert.Contains("read-only", exception.Message);
        Assert.Contains("delete ticket 5", exception.Message);
    }

    [Fact]
    public async Task InvokeWriteAsync_Translates_ZendeskApiException_When_Executing()
    {
        var exception = await Assert.ThrowsAsync<McpException>(() => ZendeskToolInvoker.InvokeWriteAsync<int>(
            Mode(McpExecutionMode.Default).Object, "update ticket 5",
            () => throw new ZendeskApiException(HttpStatusCode.Conflict, "stale", "boom")));

        Assert.Contains("409", exception.Message);
        Assert.Contains("stale", exception.Message);
    }
}
