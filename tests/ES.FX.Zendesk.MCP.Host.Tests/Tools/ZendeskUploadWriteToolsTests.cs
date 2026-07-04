using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskUploadWriteToolsTests
{
    private static (ZendeskUploadWriteTools Tools, Mock<IZendeskUploadsApi> Uploads) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var uploads = new Mock<IZendeskUploadsApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Uploads).Returns(uploads.Object);
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(m => m.EffectiveMode).Returns(mode);
        return (new ZendeskUploadWriteTools(client.Object, executionMode.Object), uploads);
    }

    [Fact]
    public async Task Create_Decodes_Base64_And_Delegates()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var expected = new ZendeskUpload { Token = "tok-1" };
        var (tools, uploads) = Create();
        uploads.Setup(api => api.UploadAsync("report.png", It.IsAny<ReadOnlyMemory<byte>>(), "image/png",
            "existing-token", It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Create("report.png", Convert.ToBase64String(bytes), "image/png",
            "existing-token", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        uploads.Verify(api => api.UploadAsync("report.png",
            It.Is<ReadOnlyMemory<byte>>(content => content.ToArray().SequenceEqual(bytes)), "image/png",
            "existing-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_Returns_Acknowledgement()
    {
        var (tools, uploads) = Create();

        var result = await tools.Delete("tok-1", TestContext.Current.CancellationToken);

        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete upload token 'tok-1'", acknowledgement.Description);
        uploads.Verify(api => api.DeleteAsync("tok-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DryRun_Returns_DryRunResult_Without_Calling_Client()
    {
        var (tools, uploads) = Create(McpExecutionMode.DryRun);

        var result = await tools.Create("report.png", Convert.ToBase64String([1, 2, 3, 4]), "image/png",
            cancellationToken: TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("upload file 'report.png'", dryRun.Description);
        Assert.NotNull(dryRun.Request);
        uploads.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadOnly_Rejects_Without_Calling_Client()
    {
        var (tools, uploads) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete("tok-1", TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        uploads.VerifyNoOtherCalls();
    }
}
