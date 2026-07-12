using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskCommunityToolsTests
{
    private static ZendeskCommunityTools CreateTools(ZendeskToolHarness harness) =>
        new(harness.CreateHelpCenterClient(), harness.CreateAdapter(),
            new StaticOptionsMonitor<McpOptions>(new McpOptions()));

    [Fact]
    public async Task Search_Requests_Community_Posts_And_Returns_Summary_Rows_Without_The_Body()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"results":[{"id":1635,"title":"Workaround for X","html_url":"https://acme.zendesk.com/hc/en-us/community/posts/1635",
             "status":"open","author_id":3465,"topic_id":7,"created_at":"2026-07-01T00:00:00Z",
             "updated_at":"2026-07-02T00:00:00Z","comment_count":4,"vote_sum":9,"pinned":false,"closed":false,
             "details":"a very long post body that should be stripped from summary rows",
             "url":"https://acme.zendesk.com/api/v2/help_center/community_posts/1635.json"}],
             "count":1,"next_page":null}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Search("workaround", cancellationToken: TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/help_center/community_posts/search.json", request.Path);
        Assert.Contains("query=workaround", request.Query);
        Assert.Contains("per_page=25", request.Query);
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        var post = result.GetProperty("items")[0];
        Assert.Equal(1635, post.GetProperty("id").GetInt64());
        Assert.Equal("Workaround for X", post.GetProperty("title").GetString());
        Assert.Equal(4, post.GetProperty("comment_count").GetInt32());
        // The post body and API self-links are stripped from summary rows.
        Assert.False(post.TryGetProperty("details", out _));
        Assert.False(post.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Search_Passes_Topic_Sort_And_Paging_Through()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"results":[],"count":0}""");
        var tools = CreateTools(harness);

        await tools.Search("bug", 42, "created_at", "desc", 2, 25,
            TestContext.Current.CancellationToken);

        var query = harness.Request.Query;
        Assert.Contains("query=bug", query);
        Assert.Contains("topic=42", query);
        Assert.Contains("sort_by=created_at", query);
        Assert.Contains("sort_order=desc", query);
        Assert.Contains("page=2", query);
    }

    [Fact]
    public async Task Search_Rejects_A_Blank_Query_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        await Assert.ThrowsAsync<McpException>(() =>
            tools.Search("   ", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(harness.Requests);
    }
}