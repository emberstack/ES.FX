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
}