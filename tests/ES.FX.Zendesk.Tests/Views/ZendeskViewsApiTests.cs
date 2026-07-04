using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.Tests.Testing;
using ES.FX.Zendesk.Views;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Zendesk.Tests.Views;

public class ZendeskViewsApiTests
{
    private static ZendeskViewsApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") },
            NullLogger<ZendeskViewsApi>.Instance);

    [Fact]
    public async Task ListAsync_Requests_Correct_Path_With_Filters_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "views": [ { "id": 1, "title": "Open tickets", "active": true } ], "meta": { "has_more": false } }""");
        var api = CreateApi(stub);

        var result = await api.ListAsync(true, 50, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.Views);
        Assert.Equal("Open tickets", result.Views[0].Title);
        Assert.False(result.Meta?.HasMore);

        var uri = stub.LastRequest!.RequestUri!;
        Assert.Equal("/api/v2/views.json", uri.AbsolutePath);
        Assert.Contains("active=true", uri.Query);
        Assert.Contains("page[size]=50", uri.Query);
    }

    [Fact]
    public async Task GetByIdAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler("""{ "view": { "id": 7, "title": "Urgent", "active": true } }""");
        var api = CreateApi(stub);

        var view = await api.GetByIdAsync(7, TestContext.Current.CancellationToken);

        Assert.Equal("Urgent", view.Title);
        Assert.Equal("https://acme.zendesk.com/api/v2/views/7.json", stub.LastRequest?.RequestUri?.ToString());
    }

    [Fact]
    public async Task GetTicketsAsync_Requests_Correct_Path_With_Sideloads()
    {
        var stub = new StubHttpMessageHandler(
            """{ "tickets": [ { "id": 9 } ], "count": 1, "users": [ { "id": 3, "name": "Agent" } ] }""");
        var api = CreateApi(stub);

        var result = await api.GetTicketsAsync(7, perPage: 25, include: ["users"],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.Tickets);
        Assert.Equal("Agent", result.Users?[0].Name);
        Assert.Equal("/api/v2/views/7/tickets.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("include=users", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetTicketCountAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler("""{ "view_count": { "view_id": 7, "value": 42, "fresh": true } }""");
        var api = CreateApi(stub);

        var count = await api.GetTicketCountAsync(7, TestContext.Current.CancellationToken);

        Assert.Equal(42, count.Value);
        Assert.True(count.Fresh);
        Assert.Equal("/api/v2/views/7/count.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Create_Uses_Write_Shape_With_Top_Level_Conditions_And_Output()
    {
        var stub = new StubHttpMessageHandler("""{ "view": { "id": 7, "title": "My open tickets" } }""");
        var view = await CreateApi(stub).CreateAsync(new ZendeskViewWrite
        {
            Title = "My open tickets",
            All = [new ZendeskViewCondition { Field = "status", Operator = "less_than", Value = "solved" }],
            Output = new ZendeskViewOutput { Columns = ["status", "requester"], SortBy = "updated_at" }
        }, TestContext.Current.CancellationToken);

        Assert.Equal(7, view.Id);
        Assert.Equal(HttpMethod.Post, stub.LastRequest!.Method);
        Assert.Equal("/api/v2/views.json", stub.LastRequest.RequestUri!.AbsolutePath);
        // Writes use the flat all/any + output shape (NOT the read-side conditions/execution shape).
        Assert.Contains("\"all\":[{\"field\":\"status\",\"operator\":\"less_than\",\"value\":\"solved\"}]",
            stub.LastRequestBody);
        Assert.Contains("\"output\":{\"columns\":[\"status\",\"requester\"],\"sort_by\":\"updated_at\"}",
            stub.LastRequestBody);
        Assert.DoesNotContain("\"conditions\"", stub.LastRequestBody);
    }

    [Fact]
    public async Task Update_And_Delete_Use_Documented_Paths()
    {
        var updateStub = new StubHttpMessageHandler("""{ "view": { "id": 7, "active": false } }""");
        await CreateApi(updateStub).UpdateAsync(7, new ZendeskViewWrite { Active = false },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Put, updateStub.LastRequest!.Method);
        Assert.Equal("/api/v2/views/7.json", updateStub.LastRequest.RequestUri!.AbsolutePath);

        var deleteStub = new StubHttpMessageHandler("");
        await CreateApi(deleteStub).DeleteAsync(7, TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Delete, deleteStub.LastRequest!.Method);
    }
}