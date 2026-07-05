using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskTagToolsTests
{
    private static ZendeskTagTools CreateTools(ZendeskToolHarness harness) =>
        new(harness.CreateSupportClient(), harness.CreateAdapter(),
            new StaticOptionsMonitor<McpOptions>(new McpOptions()));

    [Fact]
    public async Task List_Cursor_Params_Win_And_Conflicting_Offset_Params_Never_Reach_The_Wire()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"tags":[{"name":"vip","count":42}],"count":1,"next_page":null,"previous_page":null,
             "meta":{"has_more":true,"after_cursor":"cursor-2"}}
            """);
        var tools = CreateTools(harness);

        var result = await tools.List(2, 50, 100, "cursor-1", TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/tags", request.Path);
        // The regimes are mutually exclusive on the wire (DualPaginationPage: "use one format or the other,
        // not both") — the cursor params win and the conflicting page/perPage inputs are dropped.
        Assert.Contains("page%5Bsize%5D=100", request.Query);
        Assert.Contains("page%5Bafter%5D=cursor-1", request.Query);
        Assert.DoesNotContain("page=2", request.Query);
        Assert.DoesNotContain("per_page", request.Query);
        // The lean envelope: the tag rows survive intact under 'items'; the absolute paging URL strings are
        // replaced by the uniform continuation metadata (cursor regime — the call passed a cursor).
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.Equal(1, result.GetProperty("count").GetInt32());
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal("cursor-2", result.GetProperty("after_cursor").GetString());
        var tag = result.GetProperty("items")[0];
        Assert.Equal("vip", tag.GetProperty("name").GetString());
        Assert.Equal(42, tag.GetProperty("count").GetInt64());
        Assert.False(result.TryGetProperty("next_page", out _));
        Assert.False(result.TryGetProperty("previous_page", out _));
    }

    [Fact]
    public async Task List_Applies_The_Default_Cursor_Page_Size()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"tags":[],"meta":{"has_more":false}}""");
        var tools = CreateTools(harness);

        await tools.List(cancellationToken: TestContext.Current.CancellationToken);

        // The default page size is explicit on the wire (cursor regime by default) — never left to Zendesk's
        // server default of 100.
        Assert.Equal("?page%5Bsize%5D=50", harness.Request.Query);
    }

    [Fact]
    public async Task List_Offset_Paging_Applies_The_Default_Per_Page_And_Reports_The_Next_Page_Number()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"tags":[{"name":"vip","count":42}],"count":120,
             "next_page":"https://unit-test.zendesk.com/api/v2/tags.json?page=3",
             "previous_page":"https://unit-test.zendesk.com/api/v2/tags.json?page=1"}
            """);
        var tools = CreateTools(harness);

        var result = await tools.List(2, cancellationToken: TestContext.Current.CancellationToken);

        // Offset regime: the explicit per_page default rides along; no cursor parameters are added.
        var query = Uri.UnescapeDataString(harness.Request.Query);
        Assert.Contains("page=2", query);
        Assert.Contains("per_page=50", query);
        Assert.DoesNotContain("page[size]", query);
        // The envelope computes next_page as a page NUMBER — Zendesk's URL strings are never echoed.
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal(3, result.GetProperty("next_page").GetInt32());
        Assert.False(result.TryGetProperty("after_cursor", out _));
        Assert.False(result.TryGetProperty("previous_page", out _));
        Assert.Equal(120, result.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task List_Detail_Full_Returns_The_Same_Minimal_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"tags":[{"name":"vip","count":42}],"meta":{"has_more":false}}""");
        var tools = CreateTools(harness);

        var result = await tools.List(detail: "full", cancellationToken: TestContext.Current.CancellationToken);

        // Tag rows ({name, count}) are already minimal — 'full' returns the same rows, honestly labeled.
        Assert.Equal("full", result.GetProperty("detail").GetString());
        var tag = result.GetProperty("items")[0];
        Assert.Equal("vip", tag.GetProperty("name").GetString());
        Assert.Equal(42, tag.GetProperty("count").GetInt64());
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
    public async Task Count_Requests_TagCount_And_Unwraps_Envelope()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"count":{"value":1024,"refreshed_at":"2026-07-01T00:00:00Z"}}""");
        var tools = CreateTools(harness);

        var count = await tools.Count(TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/tags/count", request.Path);
        Assert.Equal(1024, count.GetProperty("value").GetInt64());
        Assert.Equal("2026-07-01T00:00:00Z", count.GetProperty("refreshed_at").GetString());
    }

    [Fact]
    public async Task Count_Throws_When_Count_Envelope_Is_Missing()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("{}");
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tools.Count(TestContext.Current.CancellationToken));

        Assert.Equal("Zendesk returned no tag count.", exception.Message);
    }

    [Fact]
    public async Task Autocomplete_Requests_Tag_Suggestions_By_Name()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"tags":["vip","vip_gold"]}""");
        var tools = CreateTools(harness);

        var result = await tools.Autocomplete("vip", TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/autocomplete/tags", request.Path);
        Assert.Contains("name=vip", request.Query);
        Assert.Equal(["vip", "vip_gold"],
            result.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()!).ToArray());
    }

    [Fact]
    public async Task Autocomplete_Defaults_Missing_Tags_To_Empty_Array()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("{}");
        var tools = CreateTools(harness);

        var result = await tools.Autocomplete("vip", TestContext.Current.CancellationToken);

        Assert.Equal(0, result.GetProperty("tags").GetArrayLength());
    }

    [Fact]
    public async Task Autocomplete_Preserves_Sibling_Wire_Properties_When_Defaulting_Tags()
    {
        var harness = new ZendeskToolHarness();
        // The tags default must patch the wire envelope, not replace it — unmodeled siblings survive.
        harness.EnqueueJson("""{"unmodeled":true}""");
        var tools = CreateTools(harness);

        var result = await tools.Autocomplete("vip", TestContext.Current.CancellationToken);

        Assert.Equal(0, result.GetProperty("tags").GetArrayLength());
        Assert.True(result.GetProperty("unmodeled").GetBoolean());
    }

    [Fact]
    public async Task Autocomplete_Rejects_Blank_Name_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.Autocomplete(" ", TestContext.Current.CancellationToken));

        Assert.Empty(harness.Requests);
    }
}