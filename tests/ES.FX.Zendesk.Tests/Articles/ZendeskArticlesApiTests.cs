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
}