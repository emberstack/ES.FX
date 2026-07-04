using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Tools;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskTagToolsTests
{
    private static (ZendeskTagTools Tools, Mock<IZendeskTagsApi> Tags) Create()
    {
        var tags = new Mock<IZendeskTagsApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Tags).Returns(tags.Object);
        return (new ZendeskTagTools(client.Object), tags);
    }

    [Fact]
    public async Task List_Delegates()
    {
        var expected = new ZendeskTagsResult();
        var (tools, tags) = Create();
        tags.Setup(api => api.ListAsync(null, null, 100, "cursor-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.List(null, null, 100, "cursor-1", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tags.Verify(api => api.ListAsync(null, null, 100, "cursor-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Count_Delegates()
    {
        var expected = new ZendeskCount();
        var (tools, tags) = Create();
        tags.Setup(api => api.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var count = await tools.Count(TestContext.Current.CancellationToken);

        Assert.Same(expected, count);
    }

    [Fact]
    public async Task Autocomplete_Delegates()
    {
        var expected = new ZendeskTagNamesResult();
        var (tools, tags) = Create();
        tags.Setup(api => api.AutocompleteAsync("vip", It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Autocomplete("vip", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tags.Verify(api => api.AutocompleteAsync("vip", It.IsAny<CancellationToken>()), Times.Once);
    }
}
