using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskViewToolsTests
{
    private static ZendeskViewTools CreateTools(ZendeskToolHarness harness) =>
        new(harness.CreateSupportClient(), harness.CreateAdapter(),
            new StaticOptionsMonitor<McpOptions>(new McpOptions()));

    [Fact]
    public async Task List_Requests_Views_With_Active_Filter_And_Returns_Summary_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"views":[{"id":9,"title":"Open tickets","active":true,"default":false,"position":3,
             "url":"https://unit-test.zendesk.com/api/v2/views/9.json",
             "conditions":{"all":[{"field":"status","operator":"less_than","value":"solved"}],"any":[]},
             "execution":{"columns":[{"id":"status","title":"Status"}],"group_by":"assignee"},
             "restriction":{"type":"Group","id":5}}],"count":1,"next_page":null,
             "previous_page":null,"meta":{"has_more":true,"after_cursor":"cur2"}}
            """);
        var tools = CreateTools(harness);

        var result = await tools.List(true, 25, "cur1", TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/views", request.Path);
        Assert.Equal("?page%5Bafter%5D=cur1&page%5Bsize%5D=25&active=true", request.Query);
        // The lean envelope: metadata first (cursor continuation), allowlisted summary rows under 'items'.
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.Equal(1, result.GetProperty("count").GetInt32());
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal("cur2", result.GetProperty("after_cursor").GetString());
        var view = result.GetProperty("items")[0];
        Assert.Equal(9, view.GetProperty("id").GetInt64());
        Assert.Equal("Open tickets", view.GetProperty("title").GetString());
        Assert.True(view.GetProperty("active").GetBoolean());
        Assert.False(view.GetProperty("default").GetBoolean());
        Assert.Equal(3, view.GetProperty("position").GetInt32());
        // Conditions/execution/restriction (the bulk of a view's tokens) are stripped from summary rows,
        // along with API self-links; views_get is the detail sink.
        Assert.False(view.TryGetProperty("conditions", out _));
        Assert.False(view.TryGetProperty("execution", out _));
        Assert.False(view.TryGetProperty("restriction", out _));
        Assert.False(view.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task List_Applies_The_Default_Page_Size()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"views":[],"meta":{"has_more":false}}""");
        var tools = CreateTools(harness);

        await tools.List(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/views", harness.Request.Path);
        // The default page size is explicit on the wire — never left to Zendesk's server default of 100.
        Assert.Equal("?page%5Bsize%5D=25", harness.Request.Query);
    }

    [Fact]
    public async Task List_Detail_Full_Returns_Unstripped_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"views":[{"id":9,"title":"Open tickets","url":"https://unit-test.zendesk.com/api/v2/views/9.json",
             "raw_title":null,"conditions":{"all":[{"field":"status","operator":"less_than","value":"solved"}]},
             "execution":{"group_by":"assignee"}}],"meta":{"has_more":false}}
            """);
        var tools = CreateTools(harness);

        var result = await tools.List(detail: "full", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var view = result.GetProperty("items")[0];
        // Full rows keep the conditions/execution the summary shape strips...
        Assert.Equal("status",
            view.GetProperty("conditions").GetProperty("all")[0].GetProperty("field").GetString());
        Assert.Equal("assignee", view.GetProperty("execution").GetProperty("group_by").GetString());
        // ...but are still the full VIEW: API self-links and null-valued fields are gone.
        Assert.False(view.TryGetProperty("url", out _));
        Assert.False(view.TryGetProperty("raw_title", out _));
    }

    [Fact]
    public async Task List_Rejects_An_Invalid_Detail_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.List(detail: "everything", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("'summary'", exception.Message);
        Assert.Contains("'full'", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Read_Requests_View_And_Preserves_Id_Conditions_And_Execution()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"view":{"id":12,"title":"Open tickets","active":true,
             "url":"https://unit-test.zendesk.com/api/v2/views/12.json","restriction":null,
             "conditions":{"all":[{"field":"status","operator":"less_than","value":"solved"}],"any":[]},
             "execution":{"columns":[{"id":"status","title":"Status"}],"group_by":"assignee","sort_by":"status"}}}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Read(12, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/views/12", request.Path);
        // views_get is the full-detail sink: the unwrapped view with its complete conditions and execution.
        Assert.Equal(12, result.GetProperty("id").GetInt64());
        Assert.Equal("Open tickets", result.GetProperty("title").GetString());
        var condition = result.GetProperty("conditions").GetProperty("all")[0];
        Assert.Equal("status", condition.GetProperty("field").GetString());
        Assert.Equal("less_than", condition.GetProperty("operator").GetString());
        Assert.Equal("solved", condition.GetProperty("value").GetString());
        Assert.Equal("assignee", result.GetProperty("execution").GetProperty("group_by").GetString());
        // The full view drops API self-links and null-valued fields (absent = null/empty).
        Assert.False(result.TryGetProperty("url", out _));
        Assert.False(result.TryGetProperty("restriction", out _));
    }

    [Fact]
    public async Task Read_Throws_When_The_View_Is_Missing()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("{}");
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(12, TestContext.Current.CancellationToken));

        Assert.Contains("'12'", exception.Message);
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task Tickets_Requests_View_Tickets_With_Paging()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"tickets":[{"id":5,"subject":"Help","custom_fields":[{"id":1,"value":"x"}]}],"count":7,
             "next_page":"https://unit-test.zendesk.com/api/v2/views/12/tickets.json?page=3"}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Tickets(12, 2, 25, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/views/12/tickets", request.Path);
        // No include: sideloads are neither OAS-modeled nor documented for this route (unverified live).
        Assert.Equal("?per_page=25&page=2", request.Query);
        // Summary ticket rows: the allowlisted triage fields stay, the token-heavy members are stripped.
        var ticket = result.GetProperty("items")[0];
        Assert.Equal(5, ticket.GetProperty("id").GetInt64());
        Assert.Equal("Help", ticket.GetProperty("subject").GetString());
        Assert.False(ticket.TryGetProperty("custom_fields", out _));
        // Offset-pagination metadata: next_page is a computed page NUMBER (request page + 1), never the URL.
        Assert.Equal(7, result.GetProperty("count").GetInt32());
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal(3, result.GetProperty("next_page").GetInt32());
    }

    [Fact]
    public async Task Tickets_Applies_The_Default_Page_Size()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"tickets":[]}""");
        var tools = CreateTools(harness);

        await tools.Tickets(12, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/views/12/tickets", harness.Request.Path);
        // perPage defaults to 25 — explicit on the wire, not left to Zendesk's server default of 100.
        Assert.Equal("?per_page=25", harness.Request.Query);
    }

    [Fact]
    public async Task Tickets_Detail_Full_Returns_Unstripped_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"tickets":[{"id":5,"subject":"Help","url":"https://unit-test.zendesk.com/api/v2/tickets/5.json",
             "custom_fields":[{"id":1,"value":"x"}],"assignee_id":null}],"count":1,"next_page":null}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Tickets(12, detail: "full",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var ticket = result.GetProperty("items")[0];
        Assert.True(ticket.TryGetProperty("custom_fields", out _)); // the complete record...
        Assert.False(ticket.TryGetProperty("url", out _)); // ...minus API self-links
        Assert.False(ticket.TryGetProperty("assignee_id", out _)); // ...and null-valued fields
    }

    [Fact]
    public async Task Tickets_Rejects_An_Invalid_Detail_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Tickets(12, detail: "everything", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("'summary'", exception.Message);
        Assert.Contains("'full'", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Count_Requests_View_Count_And_Preserves_Value_And_Freshness()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"view_count":{"view_id":12,"url":"https://unit-test.zendesk.com/api/v2/views/12/count.json",
             "value":719,"pretty":"~700","fresh":true}}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Count(12, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/views/12/count", request.Path);
        var count = result.GetProperty("view_count");
        // The generated ViewCountObject serializes nothing (all fields read-only); passthrough must keep them all.
        Assert.Equal(12, count.GetProperty("view_id").GetInt64());
        Assert.Equal(719, count.GetProperty("value").GetInt64());
        Assert.True(count.GetProperty("fresh").GetBoolean());
        Assert.Equal("~700", count.GetProperty("pretty").GetString());
    }

    [Fact]
    public async Task Execute_Returns_Columns_And_Rows_With_Summarized_Tickets()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"columns":[{"id":"subject","title":"Subject"},{"id":5,"title":"Account"}],
             "groups":[],
             "rows":[{"subject":"Broken","priority":"high",
               "ticket":{"id":42,"subject":"Broken","status":"open","description":"long body here",
                 "custom_fields":[{"id":1,"value":"secret"}],
                 "url":"https://unit-test.zendesk.com/api/v2/tickets/42.json"}}],
             "count":1,"next_page":null,
             "view":{"id":25,"title":"Unassigned"}}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Execute(25, cancellationToken: TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/views/25/execute", request.Path);
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        // Column layout metadata is preserved (the point of execute vs views_tickets_list).
        Assert.Equal("Subject", result.GetProperty("columns")[0].GetProperty("title").GetString());
        var row = result.GetProperty("items")[0];
        // Scalar column values stay...
        Assert.Equal("high", row.GetProperty("priority").GetString());
        // ...but the embedded ticket is summarized: heavy fields (custom_fields/description) and self-links dropped.
        var ticket = row.GetProperty("ticket");
        Assert.Equal(42, ticket.GetProperty("id").GetInt64());
        Assert.Equal("open", ticket.GetProperty("status").GetString());
        Assert.False(ticket.TryGetProperty("custom_fields", out _));
        Assert.False(ticket.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Execute_Passes_Sort_And_Page_And_Computes_Next_Page_Number()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"columns":[],"rows":[{"ticket":{"id":1}}],"count":60,
             "next_page":"https://unit-test.zendesk.com/api/v2/views/25/execute.json?page=3"}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Execute(25, "priority", "desc", 2, TestContext.Current.CancellationToken);

        var query = harness.Request.Query;
        Assert.Contains("sort_by=priority", query);
        Assert.Contains("sort_order=desc", query);
        Assert.Contains("page=2", query);
        Assert.True(result.GetProperty("has_more").GetBoolean());
        // The next_page URL is never echoed — a computed page NUMBER instead.
        Assert.Equal(3, result.GetProperty("next_page").GetInt32());
    }

    [Fact]
    public async Task CountMany_Requests_Ids_And_Strips_Self_Links()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"view_counts":[
              {"view_id":25,"value":42,"pretty":"42","fresh":true,
               "url":"https://unit-test.zendesk.com/api/v2/views/25/count.json"},
              {"view_id":26,"value":7,"pretty":"7","fresh":false,"url":"https://x/26"}]}
            """);
        var tools = CreateTools(harness);

        var result = await tools.CountMany([25, 26], TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/views/count_many", harness.Request.Path);
        Assert.Contains("ids=25", harness.Request.Query);
        Assert.Contains("26", harness.Request.Query);
        var counts = result.GetProperty("view_counts");
        Assert.Equal(25, counts[0].GetProperty("view_id").GetInt64());
        Assert.Equal(42, counts[0].GetProperty("value").GetInt32());
        // API self-links dropped by the full-view projection.
        Assert.False(counts[0].TryGetProperty("url", out _));
    }

    [Fact]
    public async Task CountMany_Rejects_An_Empty_Id_List_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        await Assert.ThrowsAsync<McpException>(() =>
            tools.CountMany([], TestContext.Current.CancellationToken));

        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task CountMany_Rejects_More_Than_20_Ids_Without_Calling_Zendesk()
    {
        // The endpoint accepts at most 20 view ids per request (OAS GetViewCounts) — rejected client-side with a
        // clear message instead of an opaque server 400.
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);
        var ids = Enumerable.Range(1, 21).Select(i => (long)i).ToArray();

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.CountMany(ids, TestContext.Current.CancellationToken));

        Assert.Contains("at most 20", exception.Message);
        Assert.Empty(harness.Requests);
    }
}