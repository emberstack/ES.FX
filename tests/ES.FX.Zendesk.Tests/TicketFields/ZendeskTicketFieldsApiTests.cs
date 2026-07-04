using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.Tests.Testing;
using ES.FX.Zendesk.TicketFields;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Zendesk.Tests.TicketFields;

public class ZendeskTicketFieldsApiTests
{
    private static ZendeskTicketFieldsApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") },
            NullLogger<ZendeskTicketFieldsApi>.Instance);

    [Fact]
    public async Task ListAsync_Requests_Correct_Path_And_Parses_Options()
    {
        var stub = new StubHttpMessageHandler(
            """{ "ticket_fields": [ { "id": 1, "type": "tagger", "title": "Tier", "custom_field_options": [ { "id": 9, "name": "Gold", "value": "tier_gold" } ] } ], "count": 1 }""");
        var api = CreateApi(stub);

        var result = await api.ListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.TicketFields);
        Assert.Equal("tier_gold", result.TicketFields[0].CustomFieldOptions?[0].Value);
        Assert.Equal("/api/v2/ticket_fields.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "ticket_field": { "id": 1, "type": "text", "title": "Summary" } }""");
        var api = CreateApi(stub);

        var field = await api.GetByIdAsync(1, TestContext.Current.CancellationToken);

        Assert.Equal("Summary", field.Title);
        Assert.Equal("https://acme.zendesk.com/api/v2/ticket_fields/1.json", stub.LastRequest?.RequestUri?.ToString());
    }

    [Fact]
    public async Task GetOptionsAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "custom_field_options": [ { "id": 9, "name": "Gold", "value": "tier_gold" } ], "count": 1 }""");
        var api = CreateApi(stub);

        var result = await api.GetOptionsAsync(1, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.CustomFieldOptions);
        Assert.Equal("tier_gold", result.CustomFieldOptions[0].Value);
        Assert.Equal("/api/v2/ticket_fields/1/options.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Create_Update_Delete_Use_TicketField_Envelope()
    {
        var createStub = new StubHttpMessageHandler("""{ "ticket_field": { "id": 1, "type": "tagger" } }""");
        var created = await CreateApi(createStub).CreateAsync(
            new ZendeskTicketFieldWrite { Type = "tagger", Title = "Tier" },
            TestContext.Current.CancellationToken);
        Assert.Equal(1, created.Id);
        Assert.Equal(HttpMethod.Post, createStub.LastRequest!.Method);
        Assert.Contains("\"ticket_field\":{\"type\":\"tagger\",\"title\":\"Tier\"}", createStub.LastRequestBody);

        var updateStub = new StubHttpMessageHandler("""{ "ticket_field": { "id": 1, "active": false } }""");
        await CreateApi(updateStub).UpdateAsync(1, new ZendeskTicketFieldWrite { Active = false },
            TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/ticket_fields/1.json", updateStub.LastRequest!.RequestUri!.AbsolutePath);

        var deleteStub = new StubHttpMessageHandler("");
        await CreateApi(deleteStub).DeleteAsync(1, TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Delete, deleteStub.LastRequest!.Method);
    }

    [Fact]
    public async Task Option_Upsert_And_Delete_Use_Documented_Paths()
    {
        var upsertStub = new StubHttpMessageHandler(
            """{ "custom_field_option": { "id": 9, "name": "Platinum", "value": "tier_platinum", "allow_solving": true } }""");
        var option = await CreateApi(upsertStub).CreateOrUpdateOptionAsync(1,
            new ZendeskCustomFieldOptionWrite { Name = "Platinum", Value = "tier_platinum", AllowSolving = true },
            TestContext.Current.CancellationToken);
        Assert.Equal("tier_platinum", option.Value);
        Assert.True(option.AllowSolving);
        Assert.Equal(HttpMethod.Post, upsertStub.LastRequest!.Method);
        Assert.Equal("/api/v2/ticket_fields/1/options.json", upsertStub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"custom_field_option\":{\"name\":\"Platinum\"", upsertStub.LastRequestBody);
        Assert.Contains("\"allow_solving\":true", upsertStub.LastRequestBody);

        var deleteStub = new StubHttpMessageHandler("");
        await CreateApi(deleteStub).DeleteOptionAsync(1, 9, TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/ticket_fields/1/options/9.json", deleteStub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ListAsync_Uses_Cursor_Params_And_Never_Offset()
    {
        // ticket_fields documents cursor-or-unpaginated only — offset page/per_page must never hit the wire.
        var stub = new StubHttpMessageHandler(
            """{ "ticket_fields": [ { "id": 1 } ], "meta": { "has_more": false } }""");
        var api = CreateApi(stub);

        var result = await api.ListAsync(100, "f1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Meta?.HasMore);
        Assert.Contains("page[size]=100", stub.LastRequest!.RequestUri!.Query);
        Assert.Contains("page[after]=f1", stub.LastRequest.RequestUri.Query);
        Assert.DoesNotContain("per_page", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task ListAsync_Sideloads_Creators()
    {
        var stub = new StubHttpMessageHandler(
            """{ "ticket_fields": [ { "id": 1, "title": "Tier" } ], "users": [ { "id": 7, "name": "Admin" } ] }""");
        var api = CreateApi(stub);

        var result = await api.ListAsync(include: ["users"],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("include=users", stub.LastRequest!.RequestUri!.Query);
        Assert.Equal("Admin", result.Users?[0].Name);
    }
}