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

    [Fact]
    public async Task List_Passes_Parameters_Through()
    {
        var expected = new ZendeskArticlesResult { Count = 4 };
        var include = new[] { "users", "sections" };
        var (tools, articles) = Create();
        articles.Setup(api => api.ListAsync("en-us", 9, 50, "cursor-1", include, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.List("en-us", 9, 50, "cursor-1", include, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        articles.Verify(api => api.ListAsync("en-us", 9, 50, "cursor-1", include, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Sections_Passes_Parameters_Through()
    {
        var expected = new ZendeskHelpCenterSectionsResult { Count = 3 };
        var (tools, articles) = Create();
        articles.Setup(api => api.ListSectionsAsync("en-us", 12, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.Sections("en-us", 12, 1, 100, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        articles.Verify(api => api.ListSectionsAsync("en-us", 12, 1, 100, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SectionRead_Delegates_To_GetSectionById()
    {
        var expected = new ZendeskHelpCenterSection { Id = 21, Name = "FAQ" };
        var (tools, articles) = Create();
        articles.Setup(api => api.GetSectionByIdAsync(21, "en-us", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var section = await tools.SectionRead(21, "en-us", TestContext.Current.CancellationToken);

        Assert.Same(expected, section);
        articles.Verify(api => api.GetSectionByIdAsync(21, "en-us", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Categories_Passes_Parameters_Through()
    {
        var expected = new ZendeskHelpCenterCategoriesResult { Count = 2 };
        var (tools, articles) = Create();
        articles.Setup(api => api.ListCategoriesAsync("en-us", 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.Categories("en-us", 1, 100, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        articles.Verify(api => api.ListCategoriesAsync("en-us", 1, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CategoryRead_Delegates_To_GetCategoryById()
    {
        var expected = new ZendeskHelpCenterCategory { Id = 31, Name = "Billing" };
        var (tools, articles) = Create();
        articles.Setup(api => api.GetCategoryByIdAsync(31, "en-us", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var category = await tools.CategoryRead(31, "en-us", TestContext.Current.CancellationToken);

        Assert.Same(expected, category);
        articles.Verify(api => api.GetCategoryByIdAsync(31, "en-us", It.IsAny<CancellationToken>()), Times.Once);
    }
}