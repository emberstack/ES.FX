using ES.FX.Zendesk.SuspendedTickets;
using ES.FX.Zendesk.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Zendesk.Tests.SuspendedTickets;

public class ZendeskSuspendedTicketsApiTests
{
    private static ZendeskSuspendedTicketsApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") },
            NullLogger<ZendeskSuspendedTicketsApi>.Instance);

    [Fact]
    public async Task ListAsync_Uses_Cursor_Params_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "suspended_tickets": [ { "id": 1, "subject": "Help!", "cause": "Detected as spam", "author": { "name": "Bob", "email": "bob@example.com" } } ], "meta": { "has_more": false } }""");
        var api = CreateApi(stub);

        var result = await api.ListAsync(50, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.SuspendedTickets);
        Assert.Equal("Detected as spam", result.SuspendedTickets[0].Cause);
        Assert.Equal("Bob", result.SuspendedTickets[0].Author?.Name);
        Assert.Equal("/api/v2/suspended_tickets.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("page[size]=50", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetByIdAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "suspended_ticket": { "id": 1, "subject": "Help!", "ticket_id": 99 } }""");
        var api = CreateApi(stub);

        var suspended = await api.GetByIdAsync(1, TestContext.Current.CancellationToken);

        Assert.Equal(99, suspended.TicketId);
        Assert.Equal("/api/v2/suspended_tickets/1.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task RecoverAsync_Parses_Both_Documented_Envelope_Variants()
    {
        // The OAS says { "ticket": [ ... ] } while the prose says { "tickets": [ ... ] } — both must parse.
        var oasStub = new StubHttpMessageHandler("""{ "ticket": [ { "id": 500 } ] }""");
        var oas = await CreateApi(oasStub).RecoverAsync(1, TestContext.Current.CancellationToken);
        Assert.Single(oas.Recovered);
        Assert.Equal(500, oas.Recovered[0].Id);
        Assert.Equal(HttpMethod.Put, oasStub.LastRequest!.Method);
        Assert.Equal("/api/v2/suspended_tickets/1/recover.json", oasStub.LastRequest.RequestUri!.AbsolutePath);

        var proseStub = new StubHttpMessageHandler("""{ "tickets": [ { "id": 501 }, { "id": 502 } ] }""");
        var prose = await CreateApi(proseStub).RecoverManyAsync([1, 2], TestContext.Current.CancellationToken);
        Assert.Equal(2, prose.Recovered.Count);
        Assert.Equal("/api/v2/suspended_tickets/recover_many.json", proseStub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("ids=1%2C2", proseStub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task Delete_Operations_Are_Synchronous_Deletes()
    {
        var deleteStub = new StubHttpMessageHandler("");
        await CreateApi(deleteStub).DeleteAsync(1, TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Delete, deleteStub.LastRequest!.Method);
        Assert.Equal("/api/v2/suspended_tickets/1.json", deleteStub.LastRequest.RequestUri!.AbsolutePath);

        // QUIRK: unlike tickets/destroy_many, this bulk delete is a plain 204 — no job to parse.
        var manyStub = new StubHttpMessageHandler("");
        await CreateApi(manyStub).DeleteManyAsync([1, 2], TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/suspended_tickets/destroy_many.json", manyStub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("ids=1%2C2", manyStub.LastRequest.RequestUri.Query);
    }
}