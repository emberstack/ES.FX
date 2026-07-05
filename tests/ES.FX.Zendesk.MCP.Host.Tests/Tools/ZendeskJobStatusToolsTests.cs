using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskJobStatusToolsTests
{
    private static ZendeskJobStatusTools CreateTools(ZendeskToolHarness harness) =>
        new(harness.CreateSupportClient(), harness.CreateAdapter(),
            new StaticOptionsMonitor<McpOptions>(new McpOptions()));

    [Fact]
    public async Task List_Requests_JobStatuses_With_Cursor_Pagination_And_Returns_Summary_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"job_statuses":[{"id":"job-abc","url":"https://unit-test.zendesk.com/api/v2/job_statuses/job-abc.json",
             "status":"completed","total":2,"progress":2,
             "results":[{"id":9,"index":0,"success":true},{"id":10,"index":1,"success":false,"error":"TooManyTags"}]}],
             "meta":{"has_more":true,"after_cursor":"cursor-2"}}
            """);
        var tools = CreateTools(harness);

        var result = await tools.List(100, "cursor-1", TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/job_statuses", request.Path);
        Assert.Contains("page%5Bsize%5D=100", request.Query);
        Assert.Contains("page%5Bafter%5D=cursor-1", request.Query);
        // The lean envelope: metadata first, summary rows in 'items'.
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal("cursor-2", result.GetProperty("after_cursor").GetString());
        var job = result.GetProperty("items")[0];
        Assert.Equal("job-abc", job.GetProperty("id").GetString());
        Assert.Equal("completed", job.GetProperty("status").GetString());
        // The heavy per-item results collapse to results_summary; the raw array does not appear.
        var summary = job.GetProperty("results_summary");
        Assert.Equal(1, summary.GetProperty("succeeded").GetInt32());
        Assert.Equal(1, summary.GetProperty("failed").GetInt32());
        Assert.Equal(10, summary.GetProperty("failures")[0].GetProperty("id").GetInt64());
        Assert.False(job.TryGetProperty("results", out _));
    }

    [Fact]
    public async Task List_Applies_The_Default_Page_Size()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"job_statuses":[],"meta":{"has_more":false}}""");
        var tools = CreateTools(harness);

        await tools.List(cancellationToken: TestContext.Current.CancellationToken);

        // The default page size is explicit on the wire — never left to Zendesk's server default of 100.
        Assert.Equal("?page%5Bsize%5D=20", harness.Request.Query);
    }

    [Fact]
    public async Task List_Detail_Full_Returns_Unstripped_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"job_statuses":[{"id":"job-abc","url":"https://unit-test.zendesk.com/api/v2/job_statuses/job-abc.json",
             "status":"completed","message":null,
             "results":[{"id":9,"index":0,"success":true},{"id":10,"index":1,"success":false,"error":"TooManyTags"}]}],
             "meta":{"has_more":false}}
            """);
        var tools = CreateTools(harness);

        var result = await tools.List(detail: "full", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var job = result.GetProperty("items")[0];
        // Full rows keep the complete per-item results array the summary shape collapses...
        Assert.Equal(2, job.GetProperty("results").GetArrayLength());
        Assert.Equal("TooManyTags", job.GetProperty("results")[1].GetProperty("error").GetString());
        // ...but are still the full VIEW: API self-links and null-valued fields are gone.
        Assert.False(job.TryGetProperty("url", out _));
        Assert.False(job.TryGetProperty("message", out _));
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
    public async Task Read_Requests_JobStatus_And_Returns_The_Lean_By_Status_Shape()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"job_status":{"id":"job-abc","url":"https://unit-test.zendesk.com/api/v2/job_statuses/job-abc.json",
             "status":"completed","total":3,"progress":3,"message":"Completed at 2026-07-01",
             "results":[{"id":9,"index":0,"success":true},{"id":10,"index":1,"success":false,"error":"TooManyTags"},
              {"index":2,"errors":"Invalid"}]}}
            """);
        var tools = CreateTools(harness);

        var jobStatus = await tools.Read("job-abc", TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/job_statuses/job-abc", request.Path);
        Assert.Equal("job-abc", jobStatus.GetProperty("id").GetString());
        Assert.Equal("completed", jobStatus.GetProperty("status").GetString());
        Assert.Equal(3, jobStatus.GetProperty("total").GetInt32());
        // Completed jobs carry results_summary (succeeded/failed counts + the first failures), never the raw
        // per-item results — job_statuses_get(_many) with detail:'full' is the sink for those.
        var summary = jobStatus.GetProperty("results_summary");
        Assert.Equal(1, summary.GetProperty("succeeded").GetInt32());
        Assert.Equal(2, summary.GetProperty("failed").GetInt32());
        Assert.Equal(2, summary.GetProperty("failures").GetArrayLength());
        Assert.Equal("TooManyTags", summary.GetProperty("failures")[0].GetProperty("error").GetString());
        Assert.False(jobStatus.TryGetProperty("results", out _));
        Assert.False(jobStatus.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Read_Returns_Only_The_Progress_Fields_For_A_Queued_Job()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"job_status":{"id":"job-q","status":"queued","total":5,"progress":null}}""");
        var tools = CreateTools(harness);

        var jobStatus = await tools.Read("job-q", TestContext.Current.CancellationToken);

        // Lean by status: a job with no outcome yet carries only its identity and progress fields.
        Assert.Equal("queued", jobStatus.GetProperty("status").GetString());
        Assert.Equal(5, jobStatus.GetProperty("total").GetInt32());
        Assert.False(jobStatus.TryGetProperty("progress", out _)); // null = absent
        Assert.False(jobStatus.TryGetProperty("results_summary", out _));
        Assert.False(jobStatus.TryGetProperty("message", out _));
    }

    [Fact]
    public async Task Read_Detail_Full_Returns_The_Complete_Results_Array()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"job_status":{"id":"job-abc","url":"https://unit-test.zendesk.com/api/v2/job_statuses/job-abc.json",
             "status":"completed","total":1,"progress":1,
             "results":[{"action":"update","id":9,"index":0,"status":"Updated","success":true}]}}
            """);
        var tools = CreateTools(harness);

        var jobStatus = await tools.Read("job-abc", TestContext.Current.CancellationToken, "full");

        // detail:'full' is the escalation path to the complete per-item outcomes...
        var result = jobStatus.GetProperty("results")[0];
        Assert.Equal("update", result.GetProperty("action").GetString());
        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.False(jobStatus.TryGetProperty("results_summary", out _));
        // ...still as the full VIEW: API self-links are gone.
        Assert.False(jobStatus.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Read_Rejects_An_Invalid_Detail_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read("job-abc", TestContext.Current.CancellationToken, "everything"));

        Assert.Contains("'summary'", exception.Message);
        Assert.Contains("'full'", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Read_Throws_When_JobStatus_Envelope_Is_Missing()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("{}");
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read("job-abc", TestContext.Current.CancellationToken));

        Assert.Equal("Zendesk job status 'job-abc' was not found.", exception.Message);
    }

    [Fact]
    public async Task Read_Rejects_Blank_Id_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.Read(" ", TestContext.Current.CancellationToken));

        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task ReadMany_Requests_ShowMany_With_Comma_Joined_Ids_And_Returns_Summary_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """{"job_statuses":[{"id":"job-a","status":"queued"},{"id":"job-b","status":"working"}]}""");
        var tools = CreateTools(harness);

        var result = await tools.ReadMany(["job-a", "job-b"], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/job_statuses/show_many", request.Path);
        Assert.Contains("ids=job-a%2Cjob-b", request.Query);
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        var items = result.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal("job-b", items[1].GetProperty("id").GetString());
        Assert.Equal("working", items[1].GetProperty("status").GetString());
    }

    [Fact]
    public async Task ReadMany_Detail_Full_Returns_Unstripped_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"job_statuses":[{"id":"job-a","status":"completed",
             "results":[{"id":9,"index":0,"success":false,"error":"TooManyTags"}]}]}
            """);
        var tools = CreateTools(harness);

        var result = await tools.ReadMany(["job-a"], TestContext.Current.CancellationToken, "full");

        Assert.Equal("full", result.GetProperty("detail").GetString());
        // The complete failure enumeration lives here (the summary shape caps embedded failures at 5).
        var job = result.GetProperty("items")[0];
        Assert.Equal("TooManyTags", job.GetProperty("results")[0].GetProperty("error").GetString());
        Assert.False(job.TryGetProperty("results_summary", out _));
    }

    [Fact]
    public async Task ReadMany_Returns_An_Empty_Envelope_Without_Calling_Zendesk_For_No_Ids()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var result = await tools.ReadMany([], TestContext.Current.CancellationToken);

        Assert.Empty(harness.Requests);
        Assert.Equal(0, result.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task ReadMany_Rejects_More_Than_100_Ids_With_A_Batching_Instruction()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);
        var ids = Enumerable.Range(1, 101).Select(i => $"job-{i}").ToArray();

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.ReadMany(ids, TestContext.Current.CancellationToken));

        Assert.Contains("100", exception.Message);
        Assert.Contains("101", exception.Message);
        Assert.Contains("batch", exception.Message);
        Assert.Empty(harness.Requests);
    }
}