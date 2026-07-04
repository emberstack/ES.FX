using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Tools;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskBrandToolsTests
{
    private static (ZendeskBrandTools Tools, Mock<IZendeskBrandsApi> Brands) Create()
    {
        var brands = new Mock<IZendeskBrandsApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Brands).Returns(brands.Object);
        return (new ZendeskBrandTools(client.Object), brands);
    }

    [Fact]
    public async Task List_Delegates()
    {
        var expected = new ZendeskBrandsResult();
        var (tools, brands) = Create();
        brands.Setup(api => api.ListAsync(100, "cursor-1", It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.List(100, "cursor-1", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        brands.Verify(api => api.ListAsync(100, "cursor-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Read_Delegates_To_GetById()
    {
        var expected = new ZendeskBrand { Id = 9, Name = "Acme" };
        var (tools, brands) = Create();
        brands.Setup(api => api.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var brand = await tools.Read(9, TestContext.Current.CancellationToken);

        Assert.Same(expected, brand);
    }
}
