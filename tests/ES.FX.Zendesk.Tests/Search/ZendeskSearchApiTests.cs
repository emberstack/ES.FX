using ES.FX.Zendesk.Search;
using ES.FX.Zendesk.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Zendesk.Tests.Search;

public class ZendeskSearchApiTests
{
    private static ZendeskSearchApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") },
            NullLogger<ZendeskSearchApi>.Instance);

    [Fact]
    public async Task CountAsync_Requests_Correct_Path_And_Parses_Plain_Count()
    {
        // /search/count returns a PLAIN integer count — not the { value, refreshed_at } envelope.
        var stub = new StubHttpMessageHandler("""{ "count": 1234 }""");
        var api = CreateApi(stub);

        var count = await api.CountAsync("status:open", TestContext.Current.CancellationToken);

        Assert.Equal(1234, count);
        Assert.Equal("/api/v2/search/count.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("query=status%3Aopen", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task ExportTicketsAsync_Uses_Cursor_Params_And_Type_Filter_And_Parses_Meta()
    {
        var stub = new StubHttpMessageHandler(
            """{ "results": [ { "id": 1 }, { "id": 2 } ], "meta": { "has_more": true, "after_cursor": "abc==" } }""");
        var api = CreateApi(stub);

        var results = await api.ExportTicketsAsync("status:open", 100,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Results.Count);
        Assert.True(results.Meta?.HasMore);
        Assert.Equal("abc==", results.Meta?.AfterCursor);

        var uri = stub.LastRequest!.RequestUri!;
        Assert.Equal("/api/v2/search/export.json", uri.AbsolutePath);
        Assert.Contains("query=status%3Aopen", uri.Query); // the search input must survive URI building
        Assert.Contains("filter[type]=ticket", uri.Query);
        Assert.Contains("page[size]=100", uri.Query);
    }

    [Fact]
    public async Task Search_Methods_Reject_Empty_Query()
    {
        var api = CreateApi(new StubHttpMessageHandler("{}"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            api.CountAsync(" ", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            api.ExportTicketsAsync(" ", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExportTicketsAsync_Passes_The_Continuation_Cursor()
    {
        var stub = new StubHttpMessageHandler("""{ "results": [], "meta": { "has_more": false } }""");
        var api = CreateApi(stub);

        await api.ExportTicketsAsync("status:open", afterCursor: "abc==",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("page[after]=abc%3D%3D", stub.LastRequest!.RequestUri!.Query);
    }
}