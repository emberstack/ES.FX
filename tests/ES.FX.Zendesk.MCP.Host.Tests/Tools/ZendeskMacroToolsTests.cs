using System.Text.Json;
using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskMacroToolsTests
{
    private static (ZendeskMacroTools Tools, ZendeskToolHarness Harness) Create()
    {
        var harness = new ZendeskToolHarness();
        return (new ZendeskMacroTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            new StaticOptionsMonitor<McpOptions>(new McpOptions())), harness);
    }

    [Fact]
    public async Task List_Requests_Macros_And_Returns_Summary_Rows_Without_Actions()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"macros":[{"id":1,"title":"Reply","active":true,"description":"Standard reply","usage_7d":12,
             "url":"https://unit-test.zendesk.com/api/v2/macros/1.json","restriction":null,
             "actions":[{"field":"comment_value","value":["channel","A very long canned reply body..."]}]}],
             "count":8,"next_page":null,"previous_page":null}
            """);

        var result = await tools.List(null, 25, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/macros", request.Path);
        Assert.Equal("?per_page=25", request.Query);
        // The lean envelope: metadata first, allowlisted summary rows under 'items'.
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.Equal(8, result.GetProperty("count").GetInt32());
        Assert.False(result.GetProperty("has_more").GetBoolean());
        var macro = result.GetProperty("items")[0];
        Assert.Equal(1, macro.GetProperty("id").GetInt64());
        Assert.Equal("Reply", macro.GetProperty("title").GetString());
        Assert.True(macro.GetProperty("active").GetBoolean());
        Assert.Equal("Standard reply", macro.GetProperty("description").GetString());
        Assert.Equal(12, macro.GetProperty("usage_7d").GetInt32());
        // The actions (the canned reply bodies — the bulk of a macro's tokens) are stripped from summary rows,
        // along with API self-links; macros_get is the detail sink.
        Assert.False(macro.TryGetProperty("actions", out _));
        Assert.False(macro.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task List_Passes_Page_And_PerPage_Through()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"macros":[],"count":0}""");

        await tools.List(2, 50, TestContext.Current.CancellationToken);

        Assert.Contains("page=2", harness.Request.Query);
        Assert.Contains("per_page=50", harness.Request.Query);
    }

    [Fact]
    public async Task List_Applies_The_Default_Page_Size()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"macros":[],"count":0}""");

        await tools.List(cancellationToken: TestContext.Current.CancellationToken);

        // The default page size is explicit on the wire — never left to Zendesk's server default of 100.
        Assert.Equal("?per_page=25", harness.Request.Query);
    }

    [Fact]
    public async Task List_Computes_The_Next_Page_Number_Instead_Of_Echoing_The_Url()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"macros":[{"id":1,"title":"Reply"}],"count":80,
             "next_page":"https://unit-test.zendesk.com/api/v2/macros.json?page=3"}
            """);

        var result = await tools.List(2, 25, TestContext.Current.CancellationToken);

        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal(3, result.GetProperty("next_page").GetInt32());
    }

    [Fact]
    public async Task List_Detail_Full_Returns_Unstripped_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"macros":[{"id":1,"title":"Reply","url":"https://unit-test.zendesk.com/api/v2/macros/1.json",
             "restriction":null,"actions":[{"field":"status","value":"solved"}]}],"count":1,"next_page":null}
            """);

        var result = await tools.List(null, 25, TestContext.Current.CancellationToken, "full");

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var macro = result.GetProperty("items")[0];
        // Full rows keep the actions the summary shape strips...
        Assert.Equal("solved", macro.GetProperty("actions")[0].GetProperty("value").GetString());
        // ...but are still the full VIEW: API self-links and null-valued fields are gone.
        Assert.False(macro.TryGetProperty("url", out _));
        Assert.False(macro.TryGetProperty("restriction", out _));
    }

    [Fact]
    public async Task List_Rejects_An_Invalid_Detail_Without_Calling_Zendesk()
    {
        var (tools, harness) = Create();

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.List(detail: "everything", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("'summary'", exception.Message);
        Assert.Contains("'full'", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task ListActive_Requests_Active_Endpoint_With_Paging_And_Returns_Summary_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """{"macros":[{"id":3,"title":"Thanks","actions":[{"field":"status","value":"solved"}]}],"count":3}""");

        var result = await tools.ListActive(2, 25, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/macros/active", request.Path);
        Assert.Contains("page=2", request.Query);
        Assert.Contains("per_page=25", request.Query);
        Assert.Equal(3, result.GetProperty("count").GetInt32());
        var macro = result.GetProperty("items")[0];
        Assert.Equal(3, macro.GetProperty("id").GetInt64());
        // Active-macro rows are the same lean summary shape — actions stay stripped here too.
        Assert.False(macro.TryGetProperty("actions", out _));
    }

    [Fact]
    public async Task ListActive_Omits_Null_Page()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"macros":[]}""");

        await tools.ListActive(null, 25, TestContext.Current.CancellationToken);

        Assert.Equal("?per_page=25", harness.Request.Query);
    }

    [Fact]
    public async Task ListActive_Applies_The_Default_Page_Size()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"macros":[]}""");

        await tools.ListActive(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("?per_page=25", harness.Request.Query);
    }

    [Fact]
    public async Task ListActive_Rejects_An_Invalid_Detail_Without_Calling_Zendesk()
    {
        var (tools, harness) = Create();

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.ListActive(detail: "verbose-ish", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("'summary'", exception.Message);
        Assert.Contains("'full'", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Read_Requests_Macro_By_Id_And_Preserves_Array_Action_Values()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"macro":{"id":1,"title":"Reply","url":"https://unit-test.zendesk.com/api/v2/macros/1.json",
             "restriction":null,"actions":[
                {"field":"status","value":"solved"},
                {"field":"comment_value","value":["channel","Thanks for reaching out!"]}]}}
            """);

        var result = await tools.Read(1, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/macros/1", request.Path);
        // macros_get is the full-detail sink: the unwrapped macro with its complete actions.
        Assert.Equal("Reply", result.GetProperty("title").GetString());
        var actions = result.GetProperty("actions");
        Assert.Equal("status", actions[0].GetProperty("field").GetString());
        Assert.Equal("solved", actions[0].GetProperty("value").GetString());
        // The canned reply body is an array-valued action; the raw passthrough must not drop it.
        Assert.Equal(JsonValueKind.Array, actions[1].GetProperty("value").ValueKind);
        Assert.Equal("Thanks for reaching out!", actions[1].GetProperty("value")[1].GetString());
        // The full view drops API self-links and null-valued fields (absent = null/empty).
        Assert.False(result.TryGetProperty("url", out _));
        Assert.False(result.TryGetProperty("restriction", out _));
    }

    [Fact]
    public async Task Read_Throws_When_The_Macro_Is_Missing()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("{}");

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(1, TestContext.Current.CancellationToken));

        Assert.Contains("'1'", exception.Message);
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task Search_Requests_Macros_Search_And_Returns_Summary_Rows_Without_Actions()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"macros":[{"id":7,"title":"Refund","active":true,"description":"Issue a refund","usage_30d":40,
             "url":"https://unit-test.zendesk.com/api/v2/macros/7.json",
             "actions":[{"field":"comment_value","value":["channel","A long canned refund reply..."]}]}],
             "count":1,"next_page":null}
            """);

        var result = await tools.Search("refund", cancellationToken: TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/macros/search", request.Path);
        Assert.Contains("query=refund", request.Query);
        Assert.Contains("per_page=25", request.Query);
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        var macro = result.GetProperty("items")[0];
        Assert.Equal(7, macro.GetProperty("id").GetInt64());
        Assert.Equal("Refund", macro.GetProperty("title").GetString());
        // Same lean shape as macros_list — actions and API self-links stripped.
        Assert.False(macro.TryGetProperty("actions", out _));
        Assert.False(macro.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Search_Passes_Filters_Through_To_The_Wire()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"macros":[],"count":0}""");

        await tools.Search("reset", "shared", true, 5, 12,
            true, "updated_at", "desc", 2, 50,
            TestContext.Current.CancellationToken);

        var query = harness.Request.Query;
        Assert.Contains("query=reset", query);
        Assert.Contains("access=shared", query);
        Assert.Contains("active=true", query);
        Assert.Contains("category=5", query);
        Assert.Contains("group_id=12", query);
        Assert.Contains("only_viewable=true", query);
        Assert.Contains("sort_by=updated_at", query);
        Assert.Contains("sort_order=desc", query);
        Assert.Contains("page=2", query);
        Assert.Contains("per_page=50", query);
    }

    [Fact]
    public async Task Changes_Requests_Macro_Apply_And_Unwraps_The_Result()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"result":{"ticket":{"comment":{"body":"Thanks!","public":true},
             "fields":[{"id":9,"value":"resolved"}],"status":"solved",
             "url":"https://unit-test.zendesk.com/api/v2/tickets/0.json"}}}
            """);

        var result = await tools.Changes(7, TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Get, harness.Request.Method);
        Assert.Equal("/api/v2/macros/7/apply", harness.Request.Path);
        // The 'result' wrapper is unwrapped and full-viewed: the would-be ticket changes, minus API self-links.
        var ticket = result.GetProperty("ticket");
        Assert.Equal("solved", ticket.GetProperty("status").GetString());
        Assert.Equal("Thanks!", ticket.GetProperty("comment").GetProperty("body").GetString());
        Assert.False(ticket.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Changes_Throws_When_The_Macro_Is_Missing()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("{}");

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Changes(7, TestContext.Current.CancellationToken));

        Assert.Contains("'7'", exception.Message);
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task TicketPreview_Requests_Ticket_Macro_Apply_And_Unwraps_The_Result()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"result":{"ticket":{"id":42,"subject":"Broken widget","status":"solved",
             "url":"https://unit-test.zendesk.com/api/v2/tickets/42.json"}}}
            """);

        var result = await tools.TicketPreview(42, 7, TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Get, harness.Request.Method);
        Assert.Equal("/api/v2/tickets/42/macros/7/apply", harness.Request.Path);
        var ticket = result.GetProperty("ticket");
        Assert.Equal(42, ticket.GetProperty("id").GetInt64());
        Assert.Equal("solved", ticket.GetProperty("status").GetString());
        Assert.False(ticket.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task TicketPreview_Throws_When_Missing()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("{}");

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.TicketPreview(42, 7, TestContext.Current.CancellationToken));

        Assert.Contains("not found", exception.Message);
    }
}