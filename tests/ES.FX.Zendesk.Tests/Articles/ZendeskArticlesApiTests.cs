using ES.FX.Zendesk.Articles;
using ES.FX.Zendesk.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Zendesk.Tests.Articles;

public class ZendeskArticlesApiTests
{
    private static ZendeskArticlesApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") },
            NullLogger<ZendeskArticlesApi>.Instance);

    [Fact]
    public async Task SearchAsync_Requests_HelpCenter_Path_With_Query()
    {
        var stub = new StubHttpMessageHandler(
            """{ "results": [ { "id": 1, "title": "Reset password", "snippet": "click..." } ], "count": 1 }""");
        var api = CreateApi(stub);

        var result = await api.SearchAsync("reset password", "en-us",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.Results);
        Assert.Equal("Reset password", result.Results[0].Title);

        var uri = stub.LastRequest!.RequestUri!;
        Assert.Equal("/api/v2/help_center/articles/search.json", uri.AbsolutePath);
        Assert.Contains("query=reset%20password", uri.Query);
        Assert.Contains("locale=en-us", uri.Query);
    }

    [Fact]
    public async Task GetByIdAsync_Requests_Correct_Path_And_Parses_Body()
    {
        var stub = new StubHttpMessageHandler(
            """{ "article": { "id": 5, "title": "How to", "body": "<p>steps</p>" } }""");
        var api = CreateApi(stub);

        var article = await api.GetByIdAsync(5, TestContext.Current.CancellationToken);

        Assert.Equal("How to", article.Title);
        Assert.Equal("<p>steps</p>", article.Body);
        Assert.Equal("/api/v2/help_center/articles/5.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ListAsync_Without_Locale_Uses_Agent_Path_With_Cursor_Params()
    {
        var stub = new StubHttpMessageHandler(
            """{ "articles": [ { "id": 1, "title": "Guide" } ], "meta": { "has_more": false } }""");
        var api = CreateApi(stub);

        var result = await api.ListAsync(pageSize: 50, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.Articles);
        Assert.Equal("/api/v2/help_center/articles.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("page[size]=50", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task ListAsync_With_Locale_And_Section_Scopes_The_Path()
    {
        var stub = new StubHttpMessageHandler("""{ "articles": [] }""");
        var api = CreateApi(stub);

        await api.ListAsync("en-us", 42, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/help_center/en-us/sections/42/articles.json",
            stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ListAsync_Escapes_A_Hostile_Locale()
    {
        var stub = new StubHttpMessageHandler("""{ "articles": [] }""");
        var api = CreateApi(stub);

        await api.ListAsync("../evil", cancellationToken: TestContext.Current.CancellationToken);

        // The locale is caller input interpolated into the path — it must never inject path segments.
        Assert.DoesNotContain("/../", stub.LastRequest!.RequestUri!.AbsoluteUri);
        Assert.Contains("..%2Fevil", stub.LastRequest.RequestUri.AbsoluteUri);
    }

    [Fact]
    public async Task ListSectionsAsync_Optionally_Scopes_To_A_Category()
    {
        var stub = new StubHttpMessageHandler(
            """{ "sections": [ { "id": 10, "name": "FAQ", "category_id": 3 } ], "count": 1 }""");
        var api = CreateApi(stub);

        var result = await api.ListSectionsAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(result.Sections);
        Assert.Equal("FAQ", result.Sections[0].Name);
        Assert.Equal("/api/v2/help_center/sections.json", stub.LastRequest!.RequestUri!.AbsolutePath);

        await api.ListSectionsAsync("en-us", 3, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/help_center/en-us/categories/3/sections.json",
            stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetSectionByIdAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler("""{ "section": { "id": 10, "name": "FAQ", "category_id": 3 } }""");
        var api = CreateApi(stub);

        var section = await api.GetSectionByIdAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("FAQ", section.Name);
        Assert.Equal(3, section.CategoryId);
        Assert.Equal("/api/v2/help_center/sections/10.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ListCategoriesAsync_And_GetCategoryByIdAsync_Request_Correct_Paths()
    {
        var listStub = new StubHttpMessageHandler(
            """{ "categories": [ { "id": 3, "name": "Product Help" } ], "count": 1 }""");
        var categories = await CreateApi(listStub)
            .ListCategoriesAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(categories.Categories);
        Assert.Equal("/api/v2/help_center/categories.json", listStub.LastRequest!.RequestUri!.AbsolutePath);

        var getStub = new StubHttpMessageHandler("""{ "category": { "id": 3, "name": "Product Help" } }""");
        var category = await CreateApi(getStub)
            .GetCategoryByIdAsync(3, "en-us", TestContext.Current.CancellationToken);
        Assert.Equal("Product Help", category.Name);
        Assert.Equal("/api/v2/help_center/en-us/categories/3.json", getStub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ListAsync_Sideloads_Users_Sections_And_Categories()
    {
        var stub = new StubHttpMessageHandler(
            """{ "articles": [ { "id": 1, "title": "Guide" } ], "users": [ { "id": 7, "name": "Author" } ], "sections": [ { "id": 10, "name": "FAQ" } ], "categories": [ { "id": 3, "name": "Help" } ] }""");
        var api = CreateApi(stub);

        var result = await api.ListAsync(include: ["users", "sections", "categories"],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("include=users%2Csections%2Ccategories", stub.LastRequest!.RequestUri!.Query);
        Assert.Equal("Author", result.Users?[0].Name);
        Assert.Equal("FAQ", result.Sections?[0].Name);
        Assert.Equal("Help", result.Categories?[0].Name);
    }
}