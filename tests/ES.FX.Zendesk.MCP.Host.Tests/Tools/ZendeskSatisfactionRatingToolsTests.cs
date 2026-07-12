using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskSatisfactionRatingToolsTests
{
    private static (ZendeskSatisfactionRatingTools Tools, ZendeskToolHarness Harness) Create()
    {
        var harness = new ZendeskToolHarness();
        return (new ZendeskSatisfactionRatingTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            new StaticOptionsMonitor<McpOptions>(new McpOptions())), harness);
    }

    [Fact]
    public async Task List_Requests_Ratings_And_Returns_Summary_Rows_Without_The_Self_Link()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"satisfaction_ratings":[{"id":62,"score":"bad","comment":"Slow","reason":"Took too long",
             "reason_code":100,"reason_id":5,"ticket_id":208,"requester_id":7881,"assignee_id":135,"group_id":44,
             "created_at":"2026-07-01T00:00:00Z","updated_at":"2026-07-02T00:00:00Z",
             "url":"https://unit-test.zendesk.com/api/v2/satisfaction_ratings/62.json"}],
             "count":1,"next_page":null}
            """);

        var result = await tools.List(cancellationToken: TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/satisfaction_ratings", request.Path);
        Assert.Equal("?per_page=25", request.Query);
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.Equal(1, result.GetProperty("count").GetInt32());
        var rating = result.GetProperty("items")[0];
        Assert.Equal(62, rating.GetProperty("id").GetInt64());
        Assert.Equal("bad", rating.GetProperty("score").GetString());
        Assert.Equal("Slow", rating.GetProperty("comment").GetString());
        Assert.Equal(208, rating.GetProperty("ticket_id").GetInt64());
        // The API self-link and the redundant reason_id are omitted from summary rows.
        Assert.False(rating.TryGetProperty("url", out _));
        Assert.False(rating.TryGetProperty("reason_id", out _));
    }

    [Fact]
    public async Task List_Passes_Score_And_Time_Filters_Through()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"satisfaction_ratings":[],"count":0}""");

        await tools.List("bad", 1_498_151_194, 1_500_000_000, 2, 50,
            TestContext.Current.CancellationToken);

        var query = harness.Request.Query;
        Assert.Contains("score=bad", query);
        Assert.Contains("start_time=1498151194", query);
        Assert.Contains("end_time=1500000000", query);
        Assert.Contains("page=2", query);
        Assert.Contains("per_page=50", query);
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
    public async Task Read_Requests_Rating_By_Id_And_Full_Views_It()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"satisfaction_rating":{"id":62,"score":"good","comment":"Great","reason_id":null,
             "url":"https://unit-test.zendesk.com/api/v2/satisfaction_ratings/62.json"}}
            """);

        var result = await tools.Read(62, TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/satisfaction_ratings/62", harness.Request.Path);
        Assert.Equal("good", result.GetProperty("score").GetString());
        // Full view: API self-links and null-valued fields dropped.
        Assert.False(result.TryGetProperty("url", out _));
        Assert.False(result.TryGetProperty("reason_id", out _));
    }

    [Fact]
    public async Task Read_Throws_When_The_Rating_Is_Missing()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("{}");

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(62, TestContext.Current.CancellationToken));

        Assert.Contains("'62'", exception.Message);
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task Count_Requests_The_Count_Endpoint()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"count":{"value":1234,"refreshed_at":"2026-07-02T00:00:00Z"}}""");

        var result = await tools.Count(TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/satisfaction_ratings/count", harness.Request.Path);
        Assert.Equal(1234, result.GetProperty("count").GetProperty("value").GetInt32());
    }
}