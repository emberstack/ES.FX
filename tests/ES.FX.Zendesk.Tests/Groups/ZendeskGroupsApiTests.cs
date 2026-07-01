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
}