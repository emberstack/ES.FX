using System.Net;
using ES.FX.Zendesk.Tests.Testing;

namespace ES.FX.Zendesk.Tests;

/// <summary>
///     Wire-fidelity coverage of the generated Kiota clients over a stub handler: the exact request paths and
///     percent-encoded query parameters Zendesk receives, and the error translation through the guard-wrapped
///     handler chain. These pin the contract the MCP tools and Spark consumers rely on — a regeneration that
///     changes a template or a query-parameter encoding must fail here, not in production.
/// </summary>
public class GeneratedClientWireTests
{
    [Fact]
    public async Task Tickets_GetById_Hits_The_Ticket_Path_With_The_Include_Sideload()
    {
        var harness = new ZendeskWireHarness();
        harness.EnqueueJson("""{ "ticket": { "id": 42, "subject": "Printer on fire" } }""");
        var support = harness.CreateSupportClient();

        var response = await support.Api.V2.Tickets[42].GetAsync(
            cfg => cfg.QueryParameters.Include = "users",
            TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/tickets/42", request.Path);
        Assert.Contains("include=users", request.Query);
        Assert.Equal(42, response?.Ticket?.Id); // the envelope round-tripped through the generated model
    }

    [Fact]
    public async Task HelpCenter_Article_Search_Uses_The_Json_Suffixed_Path()
    {
        var harness = new ZendeskWireHarness();
        harness.EnqueueJson("""{ "results": [], "count": 0 }""");
        var helpCenter = harness.CreateHelpCenterClient();

        await helpCenter.Api.V2.Help_center.Articles.SearchJson.GetAsync(
            cfg => cfg.QueryParameters.Query = "reset password",
            TestContext.Current.CancellationToken);

        var request = harness.Request;
        // Help Center returns HTML (or 404) without the .json suffix — the generated template must carry it.
        Assert.Equal("/api/v2/help_center/articles/search.json", request.Path);
        Assert.Contains("query=reset%20password", request.Query);
    }

    [Fact]
    public async Task Search_Export_Sends_The_Bracketed_Filter_And_Cursor_Parameters()
    {
        var harness = new ZendeskWireHarness();
        harness.EnqueueJson("""{ "results": [], "meta": { "has_more": false } }""");
        var support = harness.CreateSupportClient();

        await support.Api.V2.Search.Export.GetAsync(cfg =>
        {
            cfg.QueryParameters.Query = "status";
            cfg.QueryParameters.Filtertype = "ticket";
            cfg.QueryParameters.Pagesize = 100;
            cfg.QueryParameters.Pageafter = "cursor-1";
        }, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/search/export", request.Path);
        Assert.Contains("query=status", request.Query);
        // Zendesk expects the bracketed names percent-encoded exactly like this — a regeneration that decodes
        // them (filter[type]) or renames them silently breaks filtering and pagination.
        Assert.Contains("filter%5Btype%5D=ticket", request.Query);
        Assert.Contains("page%5Bsize%5D=100", request.Query);
        Assert.Contains("page%5Bafter%5D=cursor-1", request.Query);
    }

    [Fact]
    public async Task A_404_Through_The_Guard_Wrapped_Chain_Surfaces_ZendeskApiException()
    {
        var harness = new ZendeskWireHarness();
        harness.EnqueueStatus(HttpStatusCode.NotFound, """{"error":"RecordNotFound"}""");
        var support = harness.CreateSupportClient(true);

        // The guard handler throws BEFORE the Kiota adapter can swallow the body into its own ApiException —
        // consumers get the status AND the Zendesk error payload.
        var exception = await Assert.ThrowsAsync<ZendeskApiException>(async () =>
            await support.Api.V2.Tickets[404].GetAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Contains("RecordNotFound", exception.ResponseBody);
        Assert.Equal("/api/v2/tickets/404", harness.Request.Path); // the request itself was well-formed
    }
}