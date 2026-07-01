using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Tools;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskArticleToolsTests
{
    private static (ZendeskArticleTools Tools, Mock<IZendeskArticlesApi> Articles) Create()
    {
        var articles = new Mock<IZendeskArticlesApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Articles).Returns(articles.Object);
        return (new ZendeskArticleTools(client.Object), articles);
    }

    [Fact]
    public async Task Search_Passes_Parameters_Through()
    {
        var expected = new ZendeskArticleSearchResults { Count = 2 };
        var (tools, articles) = Create();
        articles.Setup(api => api.SearchAsync("password", "en-us", 1, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.Search("password", "en-us", 1, 25, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        articles.Verify(api => api.SearchAsync("password", "en-us", 1, 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Read_Delegates_To_GetById()
    {
        var expected = new ZendeskArticle { Id = 5, Title = "How to" };
        var (tools, articles) = Create();
        articles.Setup(api => api.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var article = await tools.Read(5, TestContext.Current.CancellationToken);

        Assert.Same(expected, article);
    }
}