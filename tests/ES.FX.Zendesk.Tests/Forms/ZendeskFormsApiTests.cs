using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.Forms;
using ES.FX.Zendesk.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Zendesk.Tests.Forms;

public class ZendeskFormsApiTests
{
    private static ZendeskFormsApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") },
            NullLogger<ZendeskFormsApi>.Instance);

    [Fact]
    public async Task ListAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "ticket_forms": [ { "id": 1, "name": "Default", "default": true }, { "id": 2, "name": "Support" } ] }""");
        var api = CreateApi(stub);

        var result = await api.ListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.TicketForms.Count);
        Assert.Equal("Default", result.TicketForms[0].Name);
        Assert.True(result.TicketForms[0].Default);
        Assert.Equal("https://acme.zendesk.com/api/v2/ticket_forms.json", stub.LastRequest?.RequestUri?.ToString());
    }

    [Fact]
    public async Task GetByIdAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "ticket_form": { "id": 7, "name": "VIP", "display_name": "VIP Support", "active": true } }""");
        var api = CreateApi(stub);

        var form = await api.GetByIdAsync(7, TestContext.Current.CancellationToken);

        Assert.Equal(7, form.Id);
        Assert.Equal("VIP Support", form.DisplayName);
        Assert.True(form.Active);
        Assert.Equal("https://acme.zendesk.com/api/v2/ticket_forms/7.json", stub.LastRequest?.RequestUri?.ToString());
    }

    [Fact]
    public async Task ListAsync_Supports_Cursor_Pagination()
    {
        var stub = new StubHttpMessageHandler(
            """{ "ticket_forms": [ { "id": 1 } ], "meta": { "has_more": true, "after_cursor": "f2" } }""");
        var api = CreateApi(stub);

        var result = await api.ListAsync(50, "f1", TestContext.Current.CancellationToken);

        Assert.True(result.Meta?.HasMore);
        Assert.Equal("f2", result.Meta?.AfterCursor);
        Assert.Contains("page[size]=50", stub.LastRequest!.RequestUri!.Query);
        Assert.Contains("page[after]=f1", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task Create_Update_Clone_And_Delete_Use_Documented_Paths()
    {
        var createStub = new StubHttpMessageHandler("""{ "ticket_form": { "id": 7, "name": "VIP" } }""");
        var created = await CreateApi(createStub).CreateAsync(
            new ZendeskTicketFormWrite { Name = "VIP", TicketFieldIds = [1, 2] },
            TestContext.Current.CancellationToken);
        Assert.Equal(7, created.Id);
        Assert.Equal(HttpMethod.Post, createStub.LastRequest!.Method);
        Assert.Contains("\"ticket_form\":{\"name\":\"VIP\",\"ticket_field_ids\":[1,2]}", createStub.LastRequestBody);

        var updateStub = new StubHttpMessageHandler("""{ "ticket_form": { "id": 7, "active": false } }""");
        await CreateApi(updateStub).UpdateAsync(7, new ZendeskTicketFormWrite { Active = false },
            TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/ticket_forms/7.json", updateStub.LastRequest!.RequestUri!.AbsolutePath);

        // Clone is a body-less POST that returns the new copy.
        var cloneStub = new StubHttpMessageHandler("""{ "ticket_form": { "id": 8, "name": "VIP (copy)" } }""");
        var clone = await CreateApi(cloneStub).CloneAsync(7, TestContext.Current.CancellationToken);
        Assert.Equal(8, clone.Id);
        Assert.Equal(HttpMethod.Post, cloneStub.LastRequest!.Method);
        Assert.Equal("/api/v2/ticket_forms/7/clone.json", cloneStub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Null(cloneStub.LastRequestBody);

        var deleteStub = new StubHttpMessageHandler("");
        await CreateApi(deleteStub).DeleteAsync(7, TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Delete, deleteStub.LastRequest!.Method);
    }
}