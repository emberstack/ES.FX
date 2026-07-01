using System.Net;
using System.Text;
using ES.FX.Zendesk.Tests.Testing;
using ES.FX.Zendesk.Users;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Zendesk.Tests.Users;

public class ZendeskUsersApiTests
{
    private static ZendeskUsersApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") },
            NullLogger<ZendeskUsersApi>.Instance);

    [Fact]
    public async Task GetCurrentUserAsync_Parses_User_And_Requests_Correct_Path()
    {
        const string json =
            """
            { "user": { "id": 42, "name": "Jane Doe", "email": "jane@example.com", "role": "admin", "active": true, "verified": true } }
            """;
        var stub = new StubHttpMessageHandler(json);
        var users = CreateApi(stub);

        var user = await users.GetCurrentUserAsync(TestContext.Current.CancellationToken);

        Assert.Equal(42, user.Id);
        Assert.Equal("Jane Doe", user.Name);
        Assert.Equal("admin", user.Role);
        Assert.Equal("https://acme.zendesk.com/api/v2/users/me.json", stub.LastRequest?.RequestUri?.ToString());
    }

    [Fact]
    public async Task GetByIdAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler("""{ "user": { "id": 42, "name": "Jane Doe" } }""");
        var users = CreateApi(stub);

        var user = await users.GetByIdAsync(42, TestContext.Current.CancellationToken);

        Assert.Equal(42, user.Id);
        Assert.Equal("https://acme.zendesk.com/api/v2/users/42.json", stub.LastRequest?.RequestUri?.ToString());
    }

    [Fact]
    public async Task SearchAsync_Builds_Query_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "users": [ { "id": 1, "email": "a@x.com" }, { "id": 2 } ], "count": 2 }""");
        var users = CreateApi(stub);

        var result = await users.SearchAsync("role:agent", 2, 50,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result.Users.Count);

        var uri = stub.LastRequest!.RequestUri!;
        Assert.Equal("/api/v2/users/search.json", uri.AbsolutePath);
        Assert.Contains("query=role%3Aagent", uri.Query);
        Assert.Contains("page=2", uri.Query);
        Assert.Contains("per_page=50", uri.Query);
    }

    [Fact]
    public async Task GetRequestedTicketsAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler("""{ "tickets": [ { "id": 100 }, { "id": 101 } ], "count": 2 }""");
        var users = CreateApi(stub);

        var result = await users.GetRequestedTicketsAsync(42, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Tickets.Count);
        Assert.Equal("/api/v2/users/42/tickets/requested.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetManyAsync_Requests_ShowMany_With_Comma_Joined_Ids_And_Parses()
    {
        var stub = new StubHttpMessageHandler("""{ "users": [ { "id": 1 }, { "id": 2 } ], "count": 2 }""");
        var users = CreateApi(stub);

        var result = await users.GetManyAsync([1, 2, 3], TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Users.Count);
        Assert.Equal("/api/v2/users/show_many.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("ids=1%2C2%2C3", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetManyAsync_Chunks_Requests_Over_100_Ids_And_Merges()
    {
        // show_many rejects >100 ids with 400 — the client must chunk instead of failing the whole batch.
        var requests = new List<string>();
        var responder = new CountingHandler(request =>
        {
            requests.Add(request.RequestUri!.Query);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "users": [ { "id": 1 }, { "id": 2 } ], "count": 2 }""",
                    Encoding.UTF8, "application/json")
            };
        });
        var users = new ZendeskUsersApi(
            new HttpClient(responder) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") },
            NullLogger<ZendeskUsersApi>.Instance);
        var ids = Enumerable.Range(1, 150).Select(i => (long)i).ToArray();

        var result = await users.GetManyAsync(ids, TestContext.Current.CancellationToken);

        Assert.Equal(2, responder.Calls); // 100 + 50, not one oversized request
        Assert.Equal(4, result.Users.Count); // merged across chunks
        Assert.Equal(4, result.Count);
        Assert.Contains("ids=1%2C", requests[0]); // first chunk starts at id 1
        Assert.DoesNotContain("101", requests[0]); // ...and stops at 100
        Assert.Contains("ids=101%2C", requests[1]); // second chunk starts at id 101
    }

    [Fact]
    public async Task GetManyAsync_Empty_Ids_Returns_Empty_Without_A_Call()
    {
        var stub = new StubHttpMessageHandler("""{ "users": [] }""");
        var users = CreateApi(stub);

        var result = await users.GetManyAsync([], TestContext.Current.CancellationToken);

        Assert.Empty(result.Users);
        Assert.Null(stub.LastRequest); // short-circuited — no HTTP call
    }

    [Fact]
    public async Task GetRequestedTicketsAsync_Sideloads_With_Flat_Include()
    {
        var stub = new StubHttpMessageHandler(
            """{ "tickets": [ { "id": 1 } ], "count": 1, "organizations": [ { "id": 9, "name": "Acme" } ] }""");
        var users = CreateApi(stub);

        var result = await users.GetRequestedTicketsAsync(42, include: ["organizations"],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("include=organizations", stub.LastRequest!.RequestUri!.Query);
        Assert.Equal("Acme", result.Organizations?[0].Name);
    }

    [Fact]
    public async Task GetCurrentUserAsync_Throws_On_Empty_Envelope()
    {
        var users = CreateApi(new StubHttpMessageHandler("{}"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await users.GetCurrentUserAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetCurrentUserAsync_Throws_ZendeskApiException_With_Status_And_Body_On_Error()
    {
        var stub = new StubHttpMessageHandler("""{ "error": "Couldn't authenticate you" }""",
            HttpStatusCode.Unauthorized);
        var users = CreateApi(stub);

        var exception = await Assert.ThrowsAsync<ZendeskApiException>(async () =>
            await users.GetCurrentUserAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Contains("authenticate", exception.ResponseBody);
    }
}