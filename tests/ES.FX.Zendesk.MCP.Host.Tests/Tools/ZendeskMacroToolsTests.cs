using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Tools;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskMacroToolsTests
{
    private static (ZendeskMacroTools Tools, Mock<IZendeskMacrosApi> Macros) Create()
    {
        var macros = new Mock<IZendeskMacrosApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Macros).Returns(macros.Object);
        return (new ZendeskMacroTools(client.Object), macros);
    }

    [Fact]
    public async Task List_Delegates()
    {
        var expected = new ZendeskMacrosResult { Count = 8 };
        var (tools, macros) = Create();
        macros.Setup(api => api.ListAsync(null, 25, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.List(null, 25, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        macros.Verify(api => api.ListAsync(null, 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Read_Delegates_To_GetById()
    {
        var expected = new ZendeskMacro { Id = 1, Title = "Reply" };
        var (tools, macros) = Create();
        macros.Setup(api => api.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var macro = await tools.Read(1, TestContext.Current.CancellationToken);

        Assert.Same(expected, macro);
    }

    [Fact]
    public async Task ListActive_Delegates()
    {
        var expected = new ZendeskMacrosResult { Count = 3 };
        var (tools, macros) = Create();
        macros.Setup(api => api.ListActiveAsync(2, 25, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.ListActive(2, 25, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        macros.Verify(api => api.ListActiveAsync(2, 25, It.IsAny<CancellationToken>()), Times.Once);
    }
}