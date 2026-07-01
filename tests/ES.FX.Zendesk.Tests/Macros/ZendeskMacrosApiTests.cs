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
}