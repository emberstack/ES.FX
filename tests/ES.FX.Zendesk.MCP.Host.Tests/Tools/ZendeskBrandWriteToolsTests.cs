using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskBrandWriteToolsTests
{
    private static (ZendeskBrandWriteTools Tools, Mock<IZendeskBrandsApi> Brands) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var brands = new Mock<IZendeskBrandsApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Brands).Returns(brands.Object);
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(m => m.EffectiveMode).Returns(mode);
        return (new ZendeskBrandWriteTools(client.Object, executionMode.Object), brands);
    }

    [Fact]
    public async Task Create_Delegates()
    {
        var write = new ZendeskBrandWrite { Name = "Acme", Subdomain = "acme" };
        var expected = new ZendeskBrand { Id = 9, Name = "Acme" };
        var (tools, brands) = Create();
        brands.Setup(api => api.CreateAsync(write, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        brands.Verify(api => api.CreateAsync(write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_Delegates()
    {
        var write = new ZendeskBrandWrite { Name = "Acme EU" };
        var expected = new ZendeskBrand { Id = 9, Name = "Acme EU" };
        var (tools, brands) = Create();
        brands.Setup(api => api.UpdateAsync(9, write, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Update(9, write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        brands.Verify(api => api.UpdateAsync(9, write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_Returns_Acknowledgement()
    {
        var (tools, brands) = Create();

        var result = await tools.Delete(9, TestContext.Current.CancellationToken);

        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete brand 9", acknowledgement.Description);
        brands.Verify(api => api.DeleteAsync(9, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DryRun_Returns_DryRunResult_Without_Calling_Client()
    {
        var write = new ZendeskBrandWrite { Name = "Acme", Subdomain = "acme" };
        var (tools, brands) = Create(McpExecutionMode.DryRun);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("create brand 'Acme'", dryRun.Description);
        Assert.Same(write, dryRun.Request);
        brands.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadOnly_Rejects_Without_Calling_Client()
    {
        var (tools, brands) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete(9, TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        brands.VerifyNoOtherCalls();
    }
}
