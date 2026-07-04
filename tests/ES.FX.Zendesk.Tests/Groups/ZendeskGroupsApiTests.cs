using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.Groups;
using ES.FX.Zendesk.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Zendesk.Tests.Groups;

public class ZendeskGroupsApiTests
{
    private static ZendeskGroupsApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") },
            NullLogger<ZendeskGroupsApi>.Instance);

    [Fact]
    public async Task ListAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "groups": [ { "id": 1, "name": "Tier 1" }, { "id": 2, "name": "Tier 2" } ], "count": 2 }""");
        var api = CreateApi(stub);

        var result = await api.ListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Groups.Count);
        Assert.Equal("Tier 1", result.Groups[0].Name);
        Assert.Equal("/api/v2/groups.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler("""{ "group": { "id": 55, "name": "Billing", "is_public": true } }""");
        var api = CreateApi(stub);

        var group = await api.GetByIdAsync(55, TestContext.Current.CancellationToken);

        Assert.Equal("Billing", group.Name);
        Assert.True(group.IsPublic);
        Assert.Equal("https://acme.zendesk.com/api/v2/groups/55.json", stub.LastRequest?.RequestUri?.ToString());
    }

    [Fact]
    public async Task GetMembershipsAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "group_memberships": [ { "id": 10, "user_id": 7, "group_id": 55 } ], "count": 1 }""");
        var api = CreateApi(stub);

        var result = await api.GetMembershipsAsync(55, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.GroupMemberships);
        Assert.Equal(7, result.GroupMemberships[0].UserId);
        Assert.Equal("/api/v2/groups/55/memberships.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetAssignableAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler("""{ "groups": [ { "id": 1, "name": "Tier 1" } ], "count": 1 }""");
        var api = CreateApi(stub);

        var result = await api.GetAssignableAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.Groups);
        Assert.Equal("/api/v2/groups/assignable.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CountAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler("""{ "count": { "value": 12 } }""");
        var api = CreateApi(stub);

        var count = await api.CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(12, count.Value);
        Assert.Equal("/api/v2/groups/count.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetUsersAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler("""{ "users": [ { "id": 7, "name": "Agent" } ], "count": 1 }""");
        var api = CreateApi(stub);

        var result = await api.GetUsersAsync(55, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.Users);
        Assert.Equal("/api/v2/groups/55/users.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Create_Update_Delete_Use_Group_Envelope_And_Documented_Paths()
    {
        var createStub = new StubHttpMessageHandler("""{ "group": { "id": 5, "name": "Tier 3" } }""");
        var created = await CreateApi(createStub).CreateAsync(
            new ZendeskGroupWrite { Name = "Tier 3", IsPublic = false }, TestContext.Current.CancellationToken);
        Assert.Equal(5, created.Id);
        Assert.Equal(HttpMethod.Post, createStub.LastRequest!.Method);
        Assert.Contains("\"group\":{\"name\":\"Tier 3\",\"is_public\":false}", createStub.LastRequestBody);

        var updateStub = new StubHttpMessageHandler("""{ "group": { "id": 5, "name": "Tier 3+" } }""");
        await CreateApi(updateStub).UpdateAsync(5, new ZendeskGroupWrite { Name = "Tier 3+" },
            TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/groups/5.json", updateStub.LastRequest!.RequestUri!.AbsolutePath);

        var deleteStub = new StubHttpMessageHandler("");
        await CreateApi(deleteStub).DeleteAsync(5, TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Delete, deleteStub.LastRequest!.Method);
    }

    [Fact]
    public async Task Membership_Writes_Project_Clean_Payloads_And_MakeDefault_Returns_Plural()
    {
        var createStub = new StubHttpMessageHandler(
            """{ "group_membership": { "id": 10, "user_id": 7, "group_id": 5 } }""");
        var membership = await CreateApi(createStub).CreateMembershipAsync(7, 5,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(10, membership.Id);
        Assert.Equal("/api/v2/group_memberships.json", createStub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("\"group_membership\":{\"user_id\":7,\"group_id\":5}", createStub.LastRequestBody);

        var manyStub = new StubHttpMessageHandler("""{ "job_status": { "id": "j1", "status": "queued" } }""");
        await CreateApi(manyStub).CreateManyMembershipsAsync(
            [new ZendeskGroupMembership { Id = 999, UserId = 7, GroupId = 5, Default = true }],
            TestContext.Current.CancellationToken);
        Assert.DoesNotContain("999", manyStub.LastRequestBody); // read-model Id never leaks
        Assert.Contains("\"group_memberships\":[{\"user_id\":7,\"group_id\":5,\"default\":true}]",
            manyStub.LastRequestBody);

        var defaultStub = new StubHttpMessageHandler(
            """{ "group_memberships": [ { "id": 10, "default": true } ] }""");
        var list = await CreateApi(defaultStub).MakeMembershipDefaultAsync(7, 10,
            TestContext.Current.CancellationToken);
        Assert.Single(list.GroupMemberships);
        Assert.Equal("/api/v2/users/7/group_memberships/10/make_default.json",
            defaultStub.LastRequest!.RequestUri!.AbsolutePath);

        var deleteManyStub = new StubHttpMessageHandler("""{ "job_status": { "id": "j2", "status": "queued" } }""");
        await CreateApi(deleteManyStub).DeleteManyMembershipsAsync([10, 11],
            TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/group_memberships/destroy_many.json",
            deleteManyStub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("ids=10%2C11", deleteManyStub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task DeleteMembershipAsync_Sends_Delete_To_Membership_Id_Path()
    {
        var stub = new StubHttpMessageHandler("");
        await CreateApi(stub).DeleteMembershipAsync(10, TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Delete, stub.LastRequest!.Method);
        Assert.Equal("/api/v2/group_memberships/10.json", stub.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ListAsync_And_GetMembershipsAsync_Sideload_Users()
    {
        var listStub = new StubHttpMessageHandler(
            """{ "groups": [ { "id": 5, "name": "Tier 2" } ], "users": [ { "id": 7, "name": "Agent" } ] }""");
        var groups = await CreateApi(listStub).ListAsync(include: ["users"],
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("include=users", listStub.LastRequest!.RequestUri!.Query);
        Assert.Equal("Agent", groups.Users?[0].Name);

        var membershipsStub = new StubHttpMessageHandler(
            """{ "group_memberships": [ { "id": 10, "user_id": 7 } ], "users": [ { "id": 7, "name": "Agent" } ] }""");
        var memberships = await CreateApi(membershipsStub).GetMembershipsAsync(5, include: ["users"],
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("include=users", membershipsStub.LastRequest!.RequestUri!.Query);
        Assert.Equal("Agent", memberships.Users?[0].Name);
    }
}