using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskTicketFieldToolsTests
{
    private static (ZendeskToolHarness Harness, ZendeskTicketFieldTools Tools) Create()
    {
        var harness = new ZendeskToolHarness();
        return (harness, new ZendeskTicketFieldTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            new StaticOptionsMonitor<McpOptions>(new McpOptions())));
    }

    [Fact]
    public async Task List_Requests_Cursor_Pagination_And_Returns_Summary_Rows()
    {
        var (harness, tools) = Create();
        harness.EnqueueJson(
            """
            {"ticket_fields":[{"id":42,"url":"https://unit-test.zendesk.com/api/v2/ticket_fields/42.json",
             "type":"tagger","title":"Tier","active":true,"required":false,
             "custom_field_options":[{"id":1,"name":"Gold","value":"gold"},{"id":2,"name":"Silver","value":"silver"}]}],
             "meta":{"has_more":true,"after_cursor":"cur2"}}
            """);

        var result = await tools.List(cancellationToken: TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/ticket_fields", request.Path);
        // The default page size is explicit on the wire (cursor pagination) — no longer the unpaginated full list.
        Assert.Equal("?page%5Bsize%5D=50", request.Query);
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal("cur2", result.GetProperty("after_cursor").GetString());
        var row = result.GetProperty("items")[0];
        Assert.Equal(42, row.GetProperty("id").GetInt64());
        Assert.Equal("Tier", row.GetProperty("title").GetString());
        // Options are stripped from summary rows and replaced by the computed count.
        Assert.Equal(2, row.GetProperty("options_count").GetInt32());
        Assert.False(row.TryGetProperty("custom_field_options", out _));
        Assert.False(row.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task List_Passes_Cursor_Paging_Parameters_Through()
    {
        var (harness, tools) = Create();
        harness.EnqueueJson("""{"ticket_fields":[],"meta":{"has_more":false}}""");

        await tools.List(100, "cursor-1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/ticket_fields", harness.Request.Path);
        Assert.Equal("?page%5Bafter%5D=cursor-1&page%5Bsize%5D=100", harness.Request.Query);
    }

    [Fact]
    public async Task List_Hides_Inactive_Fields_By_Default_And_Reports_Them_In_The_Note()
    {
        var (harness, tools) = Create();
        harness.EnqueueJson(
            """
            {"ticket_fields":[{"id":1,"type":"text","title":"Active field","active":true},
             {"id":2,"type":"text","title":"Retired field","active":false},
             {"id":3,"type":"text","title":"Also retired","active":false}],
             "meta":{"has_more":true,"after_cursor":"cur2"}}
            """);

        var result = await tools.List(cancellationToken: TestContext.Current.CancellationToken);

        // The filter is MCP-side (Zendesk has no active filter) — nothing extra reaches the wire...
        Assert.Equal("?page%5Bsize%5D=50", harness.Request.Query);
        // ...and it is applied per page AFTER fetch: rows drop, but Zendesk's paging metadata stays intact.
        var items = result.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(1, items[0].GetProperty("id").GetInt64());
        Assert.Equal("2 inactive fields hidden — pass activeOnly:false to include them",
            result.GetProperty("note").GetString());
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal("cur2", result.GetProperty("after_cursor").GetString());
    }

    [Fact]
    public async Task List_ActiveOnly_False_Returns_Inactive_Fields_Without_A_Note()
    {
        var (harness, tools) = Create();
        harness.EnqueueJson(
            """
            {"ticket_fields":[{"id":1,"type":"text","title":"Active","active":true},
             {"id":2,"type":"text","title":"Retired","active":false}],"meta":{"has_more":false}}
            """);

        var result = await tools.List(activeOnly: false, cancellationToken: TestContext.Current.CancellationToken);

        var items = result.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.False(items[1].GetProperty("active").GetBoolean());
        Assert.False(result.TryGetProperty("note", out _));
    }

    [Fact]
    public async Task List_Detail_Full_Returns_Unstripped_Rows()
    {
        var (harness, tools) = Create();
        harness.EnqueueJson(
            """
            {"ticket_fields":[{"id":42,"url":"https://unit-test.zendesk.com/api/v2/ticket_fields/42.json",
             "type":"tagger","title":"Tier","active":true,"regexp_for_validation":null,
             "custom_field_options":[{"id":1,"name":"Gold","value":"gold"}]}],"meta":{"has_more":false}}
            """);

        var result = await tools.List(detail: "full", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var row = result.GetProperty("items")[0];
        Assert.True(row.TryGetProperty("custom_field_options", out _)); // the complete record...
        Assert.False(row.TryGetProperty("url", out _)); // ...minus API self-links
        Assert.False(row.TryGetProperty("regexp_for_validation", out _)); // ...and null-valued fields
    }

    [Fact]
    public async Task List_Rejects_An_Invalid_Detail_Without_Calling_Zendesk()
    {
        var (harness, tools) = Create();

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.List(detail: "everything", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("'summary'", exception.Message);
        Assert.Contains("'full'", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Read_Returns_The_Unwrapped_Ticket_Field_As_Full_View()
    {
        var (harness, tools) = Create();
        harness.EnqueueJson(
            """
            {"ticket_field":{"id":7,"url":"https://unit-test.zendesk.com/api/v2/ticket_fields/7.json",
             "type":"tagger","title":"Severity","tag":null,
             "custom_field_options":[{"id":10,"name":"High","value":"high"}]}}
            """);

        var result = await tools.Read(7, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/ticket_fields/7", request.Path);
        // The envelope is unwrapped: the tool returns the bare ticket field as a full view.
        Assert.Equal(7, result.GetProperty("id").GetInt64());
        Assert.Equal("Severity", result.GetProperty("title").GetString());
        // Option ids must survive — agents feed them back into ticket_fields_options_create_or_update.
        Assert.Equal(10, result.GetProperty("custom_field_options")[0].GetProperty("id").GetInt64());
        // Full view: API self-links and null-valued fields are gone (absent = null/empty)...
        Assert.False(result.TryGetProperty("url", out _));
        Assert.False(result.TryGetProperty("tag", out _));
        // ...and no cap marker appears while the option set fits.
        Assert.False(result.TryGetProperty("options_truncated", out _));
    }

    [Fact]
    public async Task Read_Caps_The_Options_At_100_With_A_Marker()
    {
        var (harness, tools) = Create();
        var options = string.Join(',', Enumerable.Range(1, 150)
            .Select(i => $$"""{"id":{{i}},"name":"Option {{i}}","value":"option_{{i}}"}"""));
        harness.EnqueueJson(
            $$$"""{"ticket_field":{"id":7,"type":"tagger","title":"Country","custom_field_options":[{{{options}}}]}}""");

        var result = await tools.Read(7, TestContext.Current.CancellationToken);

        // The head of the option set is kept in order; the marker names the exact paging re-call.
        var cappedOptions = result.GetProperty("custom_field_options");
        Assert.Equal(100, cappedOptions.GetArrayLength());
        Assert.Equal(1, cappedOptions[0].GetProperty("id").GetInt64());
        Assert.Equal(100, cappedOptions[99].GetProperty("id").GetInt64());
        Assert.Equal(
            "showing 100 of 150 options — page the complete set with ticket_fields_options_list (ticketFieldId:7)",
            result.GetProperty("options_truncated").GetString());
    }

    [Fact]
    public async Task Read_Throws_When_The_Field_Is_Missing()
    {
        var (harness, tools) = Create();
        harness.EnqueueJson("{}");

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(7, TestContext.Current.CancellationToken));

        Assert.Contains("'7'", exception.Message);
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task ReadMany_Requests_Show_Many_And_Returns_Full_Rows()
    {
        var (harness, tools) = Create();
        harness.EnqueueJson(
            """
            {"ticket_fields":[{"id":1,"url":"https://unit-test.zendesk.com/api/v2/ticket_fields/1.json",
             "type":"tagger","title":"Tier","custom_field_options":[{"id":10,"name":"Gold","value":"gold"}]},
             {"id":2,"type":"text","title":"Order id","tag":null}],"count":2}
            """);

        var result = await tools.ReadMany([1, 2], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/ticket_fields/show_many", request.Path);
        Assert.Equal("?ids=1%2C2", request.Query);
        // The rows are full view — this tool is the detail sink for decoding a ticket's custom_fields.
        Assert.Equal("full", result.GetProperty("detail").GetString());
        Assert.Equal(2, result.GetProperty("count").GetInt32());
        var items = result.GetProperty("items");
        Assert.Equal("gold", items[0].GetProperty("custom_field_options")[0].GetProperty("value").GetString());
        // Still the full VIEW: API self-links and null-valued fields are gone.
        Assert.False(items[0].TryGetProperty("url", out _));
        Assert.False(items[1].TryGetProperty("tag", out _));
    }

    [Fact]
    public async Task ReadMany_Caps_Each_Fields_Options_With_A_Marker()
    {
        var (harness, tools) = Create();
        var options = string.Join(',', Enumerable.Range(1, 101)
            .Select(i => $$"""{"id":{{i}},"name":"O{{i}}","value":"o{{i}}"}"""));
        harness.EnqueueJson(
            $$"""
              {"ticket_fields":[{"id":5,"type":"tagger","title":"Country","custom_field_options":[{{options}}]}],
               "count":1}
              """);

        var result = await tools.ReadMany([5], TestContext.Current.CancellationToken);

        var row = result.GetProperty("items")[0];
        Assert.Equal(100, row.GetProperty("custom_field_options").GetArrayLength());
        Assert.Equal(
            "showing 100 of 101 options — page the complete set with ticket_fields_options_list (ticketFieldId:5)",
            row.GetProperty("options_truncated").GetString());
    }

    [Fact]
    public async Task ReadMany_Returns_An_Empty_Result_Without_Calling_Zendesk()
    {
        var (harness, tools) = Create();

        var result = await tools.ReadMany([], TestContext.Current.CancellationToken);

        Assert.Empty(harness.Requests);
        Assert.Equal(0, result.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task ReadMany_Rejects_More_Than_100_Ids_With_A_Batching_Instruction()
    {
        // show_many rejects >100 ids with a 400 — the tool surfaces the contract as an actionable batching
        // error instead of fanning out server-side (the agent controls—and pays for—each call).
        var (harness, tools) = Create();
        var ids = Enumerable.Range(1, 101).Select(i => (long)i).ToArray();

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.ReadMany(ids, TestContext.Current.CancellationToken));

        Assert.Contains("100", exception.Message);
        Assert.Contains("101", exception.Message);
        Assert.Contains("batch", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Options_Passes_Offset_Paging_Parameters()
    {
        var (harness, tools) = Create();
        harness.EnqueueJson(
            """
            {"custom_field_options":[{"id":10,"name":"High","value":"high"}],"count":5,
             "next_page":"https://unit-test.zendesk.com/api/v2/ticket_fields/42/options.json?page=2"}
            """);

        var result = await tools.Options(42, 1, 100, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/ticket_fields/42/options", request.Path);
        Assert.Contains("page=1", request.Query);
        Assert.Contains("per_page=100", request.Query);
        Assert.Equal(10, result.GetProperty("custom_field_options")[0].GetProperty("id").GetInt64());
        // count survives as the continuation signal; the next_page URL is stripped by the full view
        // (pagination self-links carry no value the agent can act on — count sizes the walk instead).
        Assert.Equal(5, result.GetProperty("count").GetInt32());
        Assert.False(result.TryGetProperty("next_page", out _));
    }

    [Fact]
    public async Task Options_Defaults_Send_Only_PerPage()
    {
        var (harness, tools) = Create();
        harness.EnqueueJson("""{"custom_field_options":[]}""");

        _ = await tools.Options(42, cancellationToken: TestContext.Current.CancellationToken);

        // page is omitted when unset; perPage defaults to 100.
        Assert.Equal("?per_page=100", harness.Request.Query);
    }
}