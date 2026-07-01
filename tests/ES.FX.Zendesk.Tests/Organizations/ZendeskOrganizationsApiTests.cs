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
}