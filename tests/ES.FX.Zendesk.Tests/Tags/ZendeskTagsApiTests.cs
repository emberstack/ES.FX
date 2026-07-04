using ES.FX.Zendesk.Tags;
using ES.FX.Zendesk.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Zendesk.Tests.Tags;

public class ZendeskTagsApiTests
{
    private static ZendeskTagsApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") },
            NullLogger<ZendeskTagsApi>.Instance);

    [Fact]
    public async Task ListAsync_Requests_Correct_Path_And_Parses_Name_Count_Pairs()
    {
        var stub = new StubHttpMessageHandler(
            """{ "tags": [ { "name": "billing", "count": 42 }, { "name": "vip", "count": 7 } ], "count": 2 }""");
        var api = CreateApi(stub);

        var result = await api.ListAsync(perPage: 100, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Tags.Count);
        Assert.Equal("billing", result.Tags[0].Name);
        Assert.Equal(42, result.Tags[0].Count);
        Assert.Equal("/api/v2/tags.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("per_page=100", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task ListAsync_Supports_Cursor_Pagination_For_The_Deep_Tail()
    {
        // Offset paging is capped at 10k records by Zendesk; the cursor form must be available for the rest.
        var stub = new StubHttpMessageHandler(
            """{ "tags": [ { "name": "vip", "count": 7 } ], "meta": { "has_more": true, "after_cursor": "t2" } }""");
        var api = CreateApi(stub);

        var result = await api.ListAsync(pageSize: 100, afterCursor: "t1",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Meta?.HasMore);
        Assert.Equal("t2", result.Meta?.AfterCursor);
        Assert.Contains("page[size]=100", stub.LastRequest!.RequestUri!.Query);
        Assert.Contains("page[after]=t1", stub.LastRequest.RequestUri.Query);
        Assert.DoesNotContain("per_page", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task CountAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler("""{ "count": { "value": 1500, "refreshed_at": null } }""");
        var api = CreateApi(stub);

        var count = await api.CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1500, count.Value);
        Assert.Equal("/api/v2/tags/count.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task AutocompleteAsync_Requests_Correct_Path_And_Parses_Plain_Strings()
    {
        // Autocomplete returns tag names as PLAIN strings — unlike the list endpoint's { name, count } objects.
        var stub = new StubHttpMessageHandler("""{ "tags": [ "billing", "billing-dispute" ] }""");
        var api = CreateApi(stub);

        var result = await api.AutocompleteAsync("bil", TestContext.Current.CancellationToken);

        Assert.Equal(["billing", "billing-dispute"], result.Tags);
        Assert.Equal("/api/v2/autocomplete/tags.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("name=bil", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task AutocompleteAsync_Rejects_Empty_Name()
    {
        var api = CreateApi(new StubHttpMessageHandler("{}"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            api.AutocompleteAsync(" ", TestContext.Current.CancellationToken));
    }
}