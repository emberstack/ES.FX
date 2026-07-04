using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.Macros;
using ES.FX.Zendesk.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Zendesk.Tests.Macros;

public class ZendeskMacrosApiTests
{
    private static ZendeskMacrosApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") },
            NullLogger<ZendeskMacrosApi>.Instance);

    [Fact]
    public async Task ListAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "macros": [ { "id": 1, "title": "Close as solved", "active": true } ], "count": 1 }""");
        var api = CreateApi(stub);

        var result = await api.ListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.Macros);
        Assert.Equal("Close as solved", result.Macros[0].Title);
        Assert.Equal("/api/v2/macros.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_Requests_Correct_Path_And_Parses_Actions()
    {
        var stub = new StubHttpMessageHandler(
            """{ "macro": { "id": 1, "title": "Reply", "active": true, "actions": [ { "field": "comment_value", "value": "Thanks!" } ] } }""");
        var api = CreateApi(stub);

        var macro = await api.GetByIdAsync(1, TestContext.Current.CancellationToken);

        Assert.Equal("Reply", macro.Title);
        Assert.Equal("comment_value", macro.Actions?[0].Field);
        Assert.Equal("https://acme.zendesk.com/api/v2/macros/1.json", stub.LastRequest?.RequestUri?.ToString());
    }

    [Fact]
    public async Task ListActiveAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "macros": [ { "id": 2, "title": "Escalate", "active": true } ], "count": 1 }""");
        var api = CreateApi(stub);

        var result = await api.ListActiveAsync(perPage: 50, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.Macros);
        Assert.Equal("Escalate", result.Macros[0].Title);
        Assert.Equal("/api/v2/macros/active.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("per_page=50", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task Create_Update_Delete_Use_Macro_Envelope_With_Actions()
    {
        var createStub = new StubHttpMessageHandler("""{ "macro": { "id": 3, "title": "Close as solved" } }""");
        var created = await CreateApi(createStub).CreateAsync(new ZendeskMacroWrite
        {
            Title = "Close as solved",
            Actions = [new ZendeskMacroActionWrite { Field = "status", Value = "solved" }]
        }, TestContext.Current.CancellationToken);
        Assert.Equal(3, created.Id);
        Assert.Equal(HttpMethod.Post, createStub.LastRequest!.Method);
        Assert.Equal("/api/v2/macros.json", createStub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"actions\":[{\"field\":\"status\",\"value\":\"solved\"}]", createStub.LastRequestBody);

        var updateStub = new StubHttpMessageHandler("""{ "macro": { "id": 3, "active": false } }""");
        await CreateApi(updateStub).UpdateAsync(3, new ZendeskMacroWrite { Active = false },
            TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/macros/3.json", updateStub.LastRequest!.RequestUri!.AbsolutePath);

        var deleteStub = new StubHttpMessageHandler("");
        await CreateApi(deleteStub).DeleteAsync(3, TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Delete, deleteStub.LastRequest!.Method);
    }
}