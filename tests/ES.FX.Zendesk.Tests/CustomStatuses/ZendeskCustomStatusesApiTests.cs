using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.CustomStatuses;
using ES.FX.Zendesk.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Zendesk.Tests.CustomStatuses;

public class ZendeskCustomStatusesApiTests
{
    private static ZendeskCustomStatusesApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") },
            NullLogger<ZendeskCustomStatusesApi>.Instance);

    [Fact]
    public async Task ListAsync_Requests_Correct_Path_With_Filters_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "custom_statuses": [ { "id": 10, "status_category": "open", "agent_label": "Investigating", "active": true } ] }""");
        var api = CreateApi(stub);

        var result = await api.ListAsync(true, statusCategories: "open,pending",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.CustomStatuses);
        Assert.Equal("Investigating", result.CustomStatuses[0].AgentLabel);
        Assert.Equal("open", result.CustomStatuses[0].StatusCategory);

        var uri = stub.LastRequest!.RequestUri!;
        Assert.Equal("/api/v2/custom_statuses.json", uri.AbsolutePath);
        Assert.Contains("active=true", uri.Query);
        Assert.Contains("status_categories=open%2Cpending", uri.Query);
    }

    [Fact]
    public async Task GetByIdAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "custom_status": { "id": 10, "status_category": "hold", "agent_label": "Waiting on vendor", "end_user_label": "In progress" } }""");
        var api = CreateApi(stub);

        var status = await api.GetByIdAsync(10, TestContext.Current.CancellationToken);

        Assert.Equal("Waiting on vendor", status.AgentLabel);
        Assert.Equal("In progress", status.EndUserLabel);
        Assert.Equal("https://acme.zendesk.com/api/v2/custom_statuses/10.json",
            stub.LastRequest?.RequestUri?.ToString());
    }

    [Fact]
    public async Task Create_Update_Delete_Use_CustomStatus_Envelope()
    {
        var createStub = new StubHttpMessageHandler(
            """{ "custom_status": { "id": 10, "status_category": "open", "agent_label": "Investigating" } }""");
        var created = await CreateApi(createStub).CreateAsync(
            new ZendeskCustomStatusWrite { StatusCategory = "open", AgentLabel = "Investigating" },
            TestContext.Current.CancellationToken);
        Assert.Equal(10, created.Id);
        Assert.Equal(HttpMethod.Post, createStub.LastRequest!.Method);
        Assert.Contains("\"custom_status\":{\"status_category\":\"open\",\"agent_label\":\"Investigating\"}",
            createStub.LastRequestBody);

        var updateStub = new StubHttpMessageHandler("""{ "custom_status": { "id": 10, "active": false } }""");
        await CreateApi(updateStub).UpdateAsync(10, new ZendeskCustomStatusWrite { Active = false },
            TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/custom_statuses/10.json", updateStub.LastRequest!.RequestUri!.AbsolutePath);

        var deleteStub = new StubHttpMessageHandler("");
        await CreateApi(deleteStub).DeleteAsync(10, TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Delete, deleteStub.LastRequest!.Method);
    }
}