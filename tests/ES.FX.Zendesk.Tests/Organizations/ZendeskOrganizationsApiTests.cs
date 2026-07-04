using System.Net;
using System.Text;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.Organizations;
using ES.FX.Zendesk.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Zendesk.Tests.Organizations;

public class ZendeskOrganizationsApiTests
{
    private static ZendeskOrganizationsApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") },
            NullLogger<ZendeskOrganizationsApi>.Instance);

    [Fact]
    public async Task GetByIdAsync_Requests_Correct_Path_And_Parses_Custom_Fields()
    {
        var stub = new StubHttpMessageHandler(
            """{ "organization": { "id": 7, "name": "Acme Inc", "group_id": 3, "organization_fields": { "tier": "gold" } } }""");
        var api = CreateApi(stub);

        var org = await api.GetByIdAsync(7, TestContext.Current.CancellationToken);

        Assert.Equal("Acme Inc", org.Name);
        Assert.Equal(3, org.GroupId);
        Assert.Equal("gold", org.OrganizationFields?["tier"].GetString());
        Assert.Equal("https://acme.zendesk.com/api/v2/organizations/7.json", stub.LastRequest?.RequestUri?.ToString());
    }

    [Fact]
    public async Task GetTicketsAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler("""{ "tickets": [ { "id": 1 }, { "id": 2 } ], "count": 2 }""");
        var api = CreateApi(stub);

        var result = await api.GetTicketsAsync(7, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Tickets.Count);
        Assert.Equal("/api/v2/organizations/7/tickets.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetTicketsAsync_Sideloads_With_Flat_Include_And_Parses_Siblings()
    {
        var stub = new StubHttpMessageHandler(
            """{ "tickets": [ { "id": 1 } ], "count": 1, "users": [ { "id": 7, "name": "Agent" } ] }""");
        var api = CreateApi(stub);

        var result = await api.GetTicketsAsync(7, include: ["users", "groups"],
            cancellationToken: TestContext.Current.CancellationToken);

        // List endpoints use a flat include list (unlike search's nested syntax).
        Assert.Contains("include=users%2Cgroups", stub.LastRequest!.RequestUri!.Query);
        Assert.Equal("Agent", result.Users?[0].Name);
    }

    [Fact]
    public async Task ListAsync_Uses_Cursor_Params_And_Parses_Meta()
    {
        var stub = new StubHttpMessageHandler(
            """{ "organizations": [ { "id": 1, "name": "Acme" } ], "meta": { "has_more": false } }""");
        var api = CreateApi(stub);

        var result = await api.ListAsync(100, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.Organizations);
        Assert.False(result.Meta?.HasMore);
        Assert.Equal("/api/v2/organizations.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("page[size]=100", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task CountAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler("""{ "count": { "value": 350 } }""");
        var api = CreateApi(stub);

        var count = await api.CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(350, count.Value);
        Assert.Equal("/api/v2/organizations/count.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetManyAsync_Requests_ShowMany_And_Chunks_Over_100_Ids()
    {
        var stub = new StubHttpMessageHandler("""{ "organizations": [ { "id": 1 }, { "id": 2 } ], "count": 2 }""");
        var api = CreateApi(stub);

        var result = await api.GetManyAsync([1, 2], TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Organizations.Count);
        Assert.Equal("/api/v2/organizations/show_many.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("ids=1%2C2", stub.LastRequest.RequestUri.Query);

        var requests = new List<string>();
        var responder = new CountingHandler(request =>
        {
            requests.Add(request.RequestUri!.Query);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "organizations": [ { "id": 1 } ], "count": 1 }""",
                    Encoding.UTF8, "application/json")
            };
        });
        var chunked = await CreateApi(responder)
            .GetManyAsync(Enumerable.Range(1, 150).Select(i => (long)i).ToArray(),
                TestContext.Current.CancellationToken);
        Assert.Equal(2, responder.Calls); // 100 + 50
        Assert.Equal(2, chunked.Organizations.Count);
        Assert.Contains("ids=1%2C", requests[0]); // chunk 1 starts at id 1...
        Assert.DoesNotContain("101", requests[0]); // ...and stops at 100
        Assert.Contains("ids=101%2C", requests[1]); // chunk 2 starts at id 101
    }

    [Fact]
    public async Task SearchAsync_By_Name_And_By_ExternalId_And_Rejects_Ambiguity()
    {
        var stub = new StubHttpMessageHandler("""{ "organizations": [ { "id": 1, "name": "Acme" } ] }""");
        var api = CreateApi(stub);

        await api.SearchAsync("Acme", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/organizations/search.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("name=Acme", stub.LastRequest.RequestUri.Query);

        await api.SearchAsync(externalId: "crm-9", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("external_id=crm-9", stub.LastRequest!.RequestUri!.Query);
        Assert.DoesNotContain("name=", stub.LastRequest.RequestUri.Query);

        // Exactly one of the two attributes — both or neither is an error.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            api.SearchAsync("Acme", "crm-9", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            api.SearchAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AutocompleteAsync_Requests_Correct_Path()
    {
        var stub = new StubHttpMessageHandler("""{ "organizations": [ { "id": 1, "name": "Acme" } ] }""");
        var api = CreateApi(stub);

        var result = await api.AutocompleteAsync("Ac", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.Organizations);
        Assert.Equal("/api/v2/organizations/autocomplete.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("name=Ac", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetUsersAsync_And_GetMembershipsAsync_Request_Correct_Paths()
    {
        var usersStub = new StubHttpMessageHandler("""{ "users": [ { "id": 3 } ], "count": 1 }""");
        var users = await CreateApi(usersStub).GetUsersAsync(7,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(users.Users);
        Assert.Equal("/api/v2/organizations/7/users.json", usersStub.LastRequest!.RequestUri!.AbsolutePath);

        var membershipsStub = new StubHttpMessageHandler(
            """{ "organization_memberships": [ { "id": 10, "user_id": 3, "organization_id": 7, "default": true } ], "count": 1 }""");
        var memberships = await CreateApi(membershipsStub).GetMembershipsAsync(7,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(memberships.OrganizationMemberships);
        Assert.True(memberships.OrganizationMemberships[0].Default);
        Assert.Equal("/api/v2/organizations/7/organization_memberships.json",
            membershipsStub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Create_Update_And_CreateOrUpdate_Use_Organization_Envelope()
    {
        var createStub = new StubHttpMessageHandler("""{ "organization": { "id": 7, "name": "Acme" } }""");
        var created = await CreateApi(createStub).CreateAsync(
            new ZendeskOrganizationWrite { Name = "Acme", DomainNames = ["acme.com"] },
            TestContext.Current.CancellationToken);
        Assert.Equal(7, created.Id);
        Assert.Equal(HttpMethod.Post, createStub.LastRequest!.Method);
        Assert.Equal("/api/v2/organizations.json", createStub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"organization\":{\"name\":\"Acme\",\"domain_names\":[\"acme.com\"]}",
            createStub.LastRequestBody);

        var upsertStub = new StubHttpMessageHandler("""{ "organization": { "id": 7 } }""");
        await CreateApi(upsertStub).CreateOrUpdateAsync(
            new ZendeskOrganizationWrite { ExternalId = "crm-7", Name = "Acme" },
            TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/organizations/create_or_update.json",
            upsertStub.LastRequest!.RequestUri!.AbsolutePath);

        var updateStub = new StubHttpMessageHandler("""{ "organization": { "id": 7, "notes": "Priority" } }""");
        await CreateApi(updateStub).UpdateAsync(7, new ZendeskOrganizationWrite { Notes = "Priority" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Put, updateStub.LastRequest!.Method);
        Assert.Equal("/api/v2/organizations/7.json", updateStub.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task MergeAsync_Uses_OrganizationMerge_Envelope_And_GetMergeAsync_Polls_It()
    {
        var mergeStub = new StubHttpMessageHandler(
            """{ "organization_merge": { "id": "01HPZM", "winner_id": 9, "loser_id": 3, "status": "new" } }""");
        var merge = await CreateApi(mergeStub).MergeAsync(3, 9, TestContext.Current.CancellationToken);
        Assert.Equal("01HPZM", merge.Id); // string id — not a job_status
        Assert.Equal(9, merge.WinnerId);
        Assert.Equal(HttpMethod.Post, mergeStub.LastRequest!.Method);
        Assert.Equal("/api/v2/organizations/3/merge.json", mergeStub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"organization_merge\":{\"winner_id\":9}", mergeStub.LastRequestBody);

        var pollStub = new StubHttpMessageHandler(
            """{ "organization_merge": { "id": "01HPZM", "status": "complete" } }""");
        var polled = await CreateApi(pollStub).GetMergeAsync("01HPZM", TestContext.Current.CancellationToken);
        Assert.Equal("complete", polled.Status);
        Assert.Equal("/api/v2/organization_merges/01HPZM.json", pollStub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Membership_Writes_Project_Clean_Payloads_And_MakeDefault_Returns_Plural()
    {
        var createStub = new StubHttpMessageHandler(
            """{ "organization_membership": { "id": 10, "user_id": 3, "organization_id": 7 } }""");
        var membership = await CreateApi(createStub).CreateMembershipAsync(3, 7, true,
            TestContext.Current.CancellationToken);
        Assert.Equal(10, membership.Id);
        Assert.Equal("/api/v2/organization_memberships.json", createStub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains(
            "\"organization_membership\":{\"user_id\":3,\"organization_id\":7,\"default\":true}",
            createStub.LastRequestBody);

        var manyStub = new StubHttpMessageHandler("""{ "job_status": { "id": "j1", "status": "queued" } }""");
        await CreateApi(manyStub).CreateManyMembershipsAsync(
            [new ZendeskOrganizationMembership { Id = 999, UserId = 3, OrganizationId = 7 }],
            TestContext.Current.CancellationToken);
        // The read-model Id must never leak into the create payload.
        Assert.DoesNotContain("999", manyStub.LastRequestBody);
        Assert.Contains("\"organization_memberships\":[{\"user_id\":3,\"organization_id\":7", manyStub.LastRequestBody);

        var defaultStub = new StubHttpMessageHandler(
            """{ "organization_memberships": [ { "id": 10, "default": true } ] }""");
        var list = await CreateApi(defaultStub).MakeMembershipDefaultAsync(3, 10,
            TestContext.Current.CancellationToken);
        Assert.Single(list.OrganizationMemberships);
        Assert.Equal("/api/v2/users/3/organization_memberships/10/make_default.json",
            defaultStub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Bulk_Writes_And_Deletes_Use_Documented_Paths_And_Envelopes()
    {
        var job = """{ "job_status": { "id": "j1", "status": "queued" } }""";

        var createManyStub = new StubHttpMessageHandler(job);
        await CreateApi(createManyStub).CreateManyAsync([new ZendeskOrganizationWrite { Name = "Acme" }],
            TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/organizations/create_many.json", createManyStub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("\"organizations\":[{\"name\":\"Acme\"}]", createManyStub.LastRequestBody);

        var bulkStub = new StubHttpMessageHandler(job);
        await CreateApi(bulkStub).UpdateManyAsync([1, 2], new ZendeskOrganizationWrite { Notes = "Priority" },
            TestContext.Current.CancellationToken);
        Assert.Contains("ids=1%2C2", bulkStub.LastRequest!.RequestUri!.Query);
        Assert.Contains("\"organization\":{\"notes\":\"Priority\"}", bulkStub.LastRequestBody);

        var batchStub = new StubHttpMessageHandler(job);
        await CreateApi(batchStub).UpdateManyAsync([new ZendeskOrganizationWrite { Id = 1, Notes = "A" }],
            TestContext.Current.CancellationToken);
        Assert.Contains("\"organizations\":[{\"id\":1,\"notes\":\"A\"}]", batchStub.LastRequestBody);
        await Assert.ThrowsAsync<ArgumentException>(() => CreateApi(batchStub)
            .UpdateManyAsync([new ZendeskOrganizationWrite { Notes = "no id" }],
                TestContext.Current.CancellationToken));

        var deleteStub = new StubHttpMessageHandler("");
        await CreateApi(deleteStub).DeleteAsync(7, TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Delete, deleteStub.LastRequest!.Method);
        Assert.Equal("/api/v2/organizations/7.json", deleteStub.LastRequest.RequestUri!.AbsolutePath);

        var deleteManyStub = new StubHttpMessageHandler(job);
        await CreateApi(deleteManyStub).DeleteManyAsync([1, 2], TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/organizations/destroy_many.json",
            deleteManyStub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Membership_Deletes_Use_Documented_Paths()
    {
        var deleteStub = new StubHttpMessageHandler("");
        await CreateApi(deleteStub).DeleteMembershipAsync(10, TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Delete, deleteStub.LastRequest!.Method);
        Assert.Equal("/api/v2/organization_memberships/10.json", deleteStub.LastRequest.RequestUri!.AbsolutePath);

        var deleteManyStub = new StubHttpMessageHandler("""{ "job_status": { "id": "j2", "status": "queued" } }""");
        await CreateApi(deleteManyStub).DeleteManyMembershipsAsync([10, 11],
            TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/organization_memberships/destroy_many.json",
            deleteManyStub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("ids=10%2C11", deleteManyStub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetUsersAsync_Passes_Include_And_GetTagsAsync_Parses_Strings()
    {
        var usersStub = new StubHttpMessageHandler(
            """{ "users": [ { "id": 3 } ], "identities": [ { "id": 1, "type": "email" } ] }""");
        var users = await CreateApi(usersStub).GetUsersAsync(7, include: ["identities"],
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("include=identities", usersStub.LastRequest!.RequestUri!.Query);
        Assert.Single(users.Identities!);

        var tagsStub = new StubHttpMessageHandler("""{ "tags": [ "enterprise" ] }""");
        var tags = await CreateApi(tagsStub).GetTagsAsync(7, TestContext.Current.CancellationToken);
        Assert.Equal(["enterprise"], tags.Tags);
        Assert.Equal("/api/v2/organizations/7/tags.json", tagsStub.LastRequest!.RequestUri!.AbsolutePath);
    }
}