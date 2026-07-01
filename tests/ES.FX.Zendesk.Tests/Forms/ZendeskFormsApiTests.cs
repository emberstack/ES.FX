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

        var result = await api.ListAsync(TestContext.Current.CancellationToken);

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
}