using System.Net;
using System.Text;
using ES.FX.Zendesk.Abstractions.Models;
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

        var result = await users.GetManyAsync([1, 2, 3], cancellationToken: TestContext.Current.CancellationToken);

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

        var result = await users.GetManyAsync(ids, cancellationToken: TestContext.Current.CancellationToken);

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

        var result = await users.GetManyAsync([], cancellationToken: TestContext.Current.CancellationToken);

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

    [Fact]
    public async Task ListAsync_Uses_Cursor_Params_With_Role_Filter_And_Parses_Meta()
    {
        var stub = new StubHttpMessageHandler(
            """{ "users": [ { "id": 1, "role": "agent" } ], "count": 1, "meta": { "has_more": true, "after_cursor": "u2" } }""");
        var users = CreateApi(stub);

        var result = await users.ListAsync("agent", 100, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.Users);
        Assert.True(result.Meta?.HasMore);

        var uri = stub.LastRequest!.RequestUri!;
        Assert.Equal("/api/v2/users.json", uri.AbsolutePath);
        Assert.Contains("role=agent", uri.Query);
        Assert.Contains("page[size]=100", uri.Query);
    }

    [Fact]
    public async Task CountAsync_Requests_Correct_Path_With_Role()
    {
        var stub = new StubHttpMessageHandler("""{ "count": { "value": 250 } }""");
        var users = CreateApi(stub);

        var count = await users.CountAsync("end-user", TestContext.Current.CancellationToken);

        Assert.Equal(250, count.Value);
        Assert.Equal("/api/v2/users/count.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("role=end-user", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task AutocompleteAsync_Requests_Correct_Path_And_Rejects_Empty_Name()
    {
        var stub = new StubHttpMessageHandler("""{ "users": [ { "id": 1, "name": "Jan" } ], "count": 1 }""");
        var users = CreateApi(stub);

        var result = await users.AutocompleteAsync("ja", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.Users);
        Assert.Equal("/api/v2/users/autocomplete.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("name=ja", stub.LastRequest.RequestUri.Query);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            users.AutocompleteAsync(" ", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetRelatedInformationAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "user_related": { "assigned_tickets": 12, "requested_tickets": 5, "ccd_tickets": 2, "organization_subscriptions": 1 } }""");
        var users = CreateApi(stub);

        var related = await users.GetRelatedInformationAsync(42, TestContext.Current.CancellationToken);

        Assert.Equal(12, related.AssignedTickets);
        Assert.Equal(5, related.RequestedTickets);
        Assert.Equal("/api/v2/users/42/related.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetIdentitiesAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "identities": [ { "id": 1, "user_id": 42, "type": "email", "value": "jane@example.com", "verified": true, "primary": true } ], "count": 1 }""");
        var users = CreateApi(stub);

        var result = await users.GetIdentitiesAsync(42, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.Identities);
        Assert.Equal("email", result.Identities[0].Type);
        Assert.Equal("jane@example.com", result.Identities[0].Value);
        Assert.True(result.Identities[0].Primary);
        Assert.Equal("/api/v2/users/42/identities.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetGroupsAsync_And_GetOrganizationsAsync_Request_Correct_Paths()
    {
        var groupsStub = new StubHttpMessageHandler("""{ "groups": [ { "id": 5, "name": "Tier 2" } ], "count": 1 }""");
        var groups = await CreateApi(groupsStub).GetGroupsAsync(42,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(groups.Groups);
        Assert.Equal("/api/v2/users/42/groups.json", groupsStub.LastRequest!.RequestUri!.AbsolutePath);

        var orgsStub = new StubHttpMessageHandler(
            """{ "organizations": [ { "id": 9, "name": "Acme" } ], "count": 1 }""");
        var orgs = await CreateApi(orgsStub).GetOrganizationsAsync(42,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(orgs.Organizations);
        Assert.Equal("Acme", orgs.Organizations[0].Name);
        Assert.Equal("/api/v2/users/42/organizations.json", orgsStub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetAssignedTicketsAsync_And_GetCcdTicketsAsync_Request_Correct_Paths()
    {
        var assignedStub = new StubHttpMessageHandler("""{ "tickets": [ { "id": 1 } ], "count": 1 }""");
        var assigned = await CreateApi(assignedStub).GetAssignedTicketsAsync(42,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(assigned.Tickets);
        Assert.Equal("/api/v2/users/42/tickets/assigned.json", assignedStub.LastRequest!.RequestUri!.AbsolutePath);

        var ccdStub = new StubHttpMessageHandler("""{ "tickets": [ { "id": 2 } ], "count": 1 }""");
        var ccd = await CreateApi(ccdStub).GetCcdTicketsAsync(42, include: ["users"],
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(ccd.Tickets);
        Assert.Equal("/api/v2/users/42/tickets/ccd.json", ccdStub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("include=users", ccdStub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task CreateAsync_Posts_User_Envelope_And_Omits_Unset_Fields()
    {
        var stub = new StubHttpMessageHandler("""{ "user": { "id": 7, "name": "Jane" } }""");
        var user = await CreateApi(stub).CreateAsync(
            new ZendeskUserWrite { Name = "Jane", Email = "jane@example.com" },
            TestContext.Current.CancellationToken);

        Assert.Equal(7, user.Id);
        Assert.Equal(HttpMethod.Post, stub.LastRequest!.Method);
        Assert.Equal("/api/v2/users.json", stub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"user\":{\"name\":\"Jane\",\"email\":\"jane@example.com\"}", stub.LastRequestBody);
    }

    [Fact]
    public async Task CreateOrUpdateAsync_And_UpdateAsync_Use_Documented_Paths()
    {
        var upsertStub = new StubHttpMessageHandler("""{ "user": { "id": 7 } }""");
        await CreateApi(upsertStub).CreateOrUpdateAsync(new ZendeskUserWrite { Name = "Jane" },
            TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/users/create_or_update.json", upsertStub.LastRequest!.RequestUri!.AbsolutePath);

        var updateStub = new StubHttpMessageHandler("""{ "user": { "id": 7, "suspended": true } }""");
        await CreateApi(updateStub).UpdateAsync(7, new ZendeskUserWrite { Suspended = true },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Put, updateStub.LastRequest!.Method);
        Assert.Equal("/api/v2/users/7.json", updateStub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"suspended\":true", updateStub.LastRequestBody);
    }

    [Fact]
    public async Task UpdateManyAsync_Bulk_And_Batch_Forms_Behave_Like_Tickets()
    {
        var job = """{ "job_status": { "id": "j1", "status": "queued" } }""";
        var bulkStub = new StubHttpMessageHandler(job);
        await CreateApi(bulkStub).UpdateManyAsync([1, 2], new ZendeskUserWrite { OrganizationId = 9 },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Put, bulkStub.LastRequest!.Method);
        Assert.Equal("/api/v2/users/update_many.json", bulkStub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("ids=1%2C2", bulkStub.LastRequest.RequestUri.Query);
        Assert.Contains("\"user\":{\"organization_id\":9}", bulkStub.LastRequestBody);

        var batchStub = new StubHttpMessageHandler(job);
        var batch = await CreateApi(batchStub).UpdateManyAsync(
            [new ZendeskUserWrite { Id = 7, Suspended = true }], TestContext.Current.CancellationToken);
        Assert.Equal("queued", batch.Status);
        Assert.Equal(HttpMethod.Put, batchStub.LastRequest!.Method);
        Assert.Equal("/api/v2/users/update_many.json", batchStub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Equal("", batchStub.LastRequest.RequestUri.Query); // batch form has no ids query
        Assert.Contains("\"users\":[{\"id\":7,\"suspended\":true}]", batchStub.LastRequestBody);

        await Assert.ThrowsAsync<ArgumentException>(() => CreateApi(new StubHttpMessageHandler(job))
            .UpdateManyAsync([new ZendeskUserWrite { Name = "no id" }], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MergeAsync_Puts_Winner_In_Body_And_Loser_In_Path()
    {
        var stub = new StubHttpMessageHandler("""{ "user": { "id": 9, "name": "Winner" } }""");
        var winner = await CreateApi(stub).MergeAsync(5, 9, TestContext.Current.CancellationToken);

        Assert.Equal(9, winner.Id);
        Assert.Equal(HttpMethod.Put, stub.LastRequest!.Method);
        Assert.Equal("/api/v2/users/5/merge.json", stub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"user\":{\"id\":9}", stub.LastRequestBody);
    }

    [Fact]
    public async Task DeleteAsync_Returns_The_SoftDeleted_User_And_Permanent_Delete_Uses_DeletedUsers_Path()
    {
        var deleteStub = new StubHttpMessageHandler("""{ "user": { "id": 7, "active": false } }""");
        var deleted = await CreateApi(deleteStub).DeleteAsync(7, TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Delete, deleteStub.LastRequest!.Method);
        Assert.Equal("/api/v2/users/7.json", deleteStub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Equal(7, deleted.Id);

        var purgeStub = new StubHttpMessageHandler("""{ "deleted_user": { "id": 7, "name": "Jane" } }""");
        var purged = await CreateApi(purgeStub).DeletePermanentlyAsync(7, TestContext.Current.CancellationToken);
        Assert.Equal(7, purged.Id);
        Assert.Equal("/api/v2/deleted_users/7.json", purgeStub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Identity_Writes_Use_Documented_Paths_And_Envelopes()
    {
        var createStub = new StubHttpMessageHandler(
            """{ "identity": { "id": 3, "type": "email", "value": "j2@example.com" } }""");
        var identity = await CreateApi(createStub).CreateIdentityAsync(42,
            new ZendeskUserIdentityWrite { Type = "email", Value = "j2@example.com" },
            TestContext.Current.CancellationToken);
        Assert.Equal("email", identity.Type);
        Assert.Equal("/api/v2/users/42/identities.json", createStub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("\"identity\":{\"type\":\"email\"", createStub.LastRequestBody);

        // make_primary is collection-level — the response is the PLURAL identities list.
        var primaryStub = new StubHttpMessageHandler(
            """{ "identities": [ { "id": 3, "primary": true }, { "id": 2, "primary": false } ] }""");
        var identities = await CreateApi(primaryStub).MakeIdentityPrimaryAsync(42, 3,
            TestContext.Current.CancellationToken);
        Assert.Equal(2, identities.Identities.Count);
        Assert.Equal("/api/v2/users/42/identities/3/make_primary.json",
            primaryStub.LastRequest!.RequestUri!.AbsolutePath);

        var verifyStub = new StubHttpMessageHandler("""{ "identity": { "id": 3, "verified": true } }""");
        var verified = await CreateApi(verifyStub).VerifyIdentityAsync(42, 3,
            TestContext.Current.CancellationToken);
        Assert.True(verified.Verified);
        Assert.Equal(HttpMethod.Put, verifyStub.LastRequest!.Method); // body-less PUT
        Assert.Equal("/api/v2/users/42/identities/3/verify.json",
            verifyStub.LastRequest.RequestUri!.AbsolutePath);

        var requestStub = new StubHttpMessageHandler("");
        await CreateApi(requestStub).RequestIdentityVerificationAsync(42, 3,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Put, requestStub.LastRequest!.Method); // PUT, not POST
        Assert.Equal("/api/v2/users/42/identities/3/request_verification.json",
            requestStub.LastRequest.RequestUri!.AbsolutePath);

        var deleteStub = new StubHttpMessageHandler("");
        await CreateApi(deleteStub).DeleteIdentityAsync(42, 3, TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Delete, deleteStub.LastRequest!.Method);
    }

    [Fact]
    public async Task ListAsync_And_GetManyAsync_Sideload_And_Parse_Siblings()
    {
        var listStub = new StubHttpMessageHandler(
            """{ "users": [ { "id": 1, "organization_id": 9 } ], "organizations": [ { "id": 9, "name": "Acme" } ], "identities": [ { "id": 3, "type": "email", "value": "a@x.com" } ] }""");
        var list = await CreateApi(listStub).ListAsync(include: ["organizations", "identities"],
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("include=organizations%2Cidentities", listStub.LastRequest!.RequestUri!.Query);
        Assert.Equal("Acme", list.Organizations?[0].Name);
        Assert.Equal("a@x.com", list.Identities?[0].Value);

        var manyStub = new StubHttpMessageHandler(
            """{ "users": [ { "id": 1 } ], "groups": [ { "id": 5, "name": "Tier 2" } ] }""");
        var many = await CreateApi(manyStub).GetManyAsync([1], ["groups"],
            TestContext.Current.CancellationToken);
        Assert.Contains("include=groups", manyStub.LastRequest!.RequestUri!.Query);
        Assert.Equal("Tier 2", many.Groups?[0].Name);
    }

    [Fact]
    public async Task Bulk_Create_And_Delete_Operations_Return_Jobs()
    {
        var job = """{ "job_status": { "id": "j1", "status": "queued" } }""";

        var createStub = new StubHttpMessageHandler(job);
        await CreateApi(createStub).CreateManyAsync([new ZendeskUserWrite { Name = "Jane" }],
            TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/users/create_many.json", createStub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("\"users\":[{\"name\":\"Jane\"}]", createStub.LastRequestBody);

        var upsertStub = new StubHttpMessageHandler(job);
        await CreateApi(upsertStub).CreateOrUpdateManyAsync(
            [new ZendeskUserWrite { Name = "Jane", ExternalId = "crm-1" }],
            TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/users/create_or_update_many.json", upsertStub.LastRequest!.RequestUri!.AbsolutePath);

        var deleteStub = new StubHttpMessageHandler(job);
        await CreateApi(deleteStub).DeleteManyAsync([1, 2], TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Delete, deleteStub.LastRequest!.Method);
        Assert.Equal("/api/v2/users/destroy_many.json", deleteStub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("ids=1%2C2", deleteStub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task UpdateIdentityAsync_Puts_Identity_Envelope()
    {
        var stub = new StubHttpMessageHandler(
            """{ "identity": { "id": 3, "value": "new@example.com", "verified": true } }""");
        var identity = await CreateApi(stub).UpdateIdentityAsync(42, 3,
            new ZendeskUserIdentityWrite { Value = "new@example.com", Verified = true },
            TestContext.Current.CancellationToken);

        Assert.Equal("new@example.com", identity.Value);
        Assert.Equal(HttpMethod.Put, stub.LastRequest!.Method);
        Assert.Equal("/api/v2/users/42/identities/3.json", stub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"identity\":{\"value\":\"new@example.com\",\"verified\":true}", stub.LastRequestBody);
    }

    [Fact]
    public async Task GetTagsAsync_Requests_Correct_Path_And_Parses_Plain_Strings()
    {
        var stub = new StubHttpMessageHandler("""{ "tags": [ "vip", "beta-tester" ] }""");
        var users = CreateApi(stub);

        var result = await users.GetTagsAsync(42, TestContext.Current.CancellationToken);

        Assert.Equal(["vip", "beta-tester"], result.Tags);
        Assert.Equal("/api/v2/users/42/tags.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }
}