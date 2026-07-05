using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskCustomStatusToolsTests
{
    private static (ZendeskCustomStatusTools Tools, ZendeskToolHarness Harness) Create()
    {
        var harness = new ZendeskToolHarness();
        return (new ZendeskCustomStatusTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            new StaticOptionsMonitor<McpOptions>(new McpOptions())), harness);
    }

    [Fact]
    public async Task List_Sends_Filters_And_Returns_Summary_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"custom_statuses":[{"id":31,"status_category":"open","agent_label":"Investigating",
            "end_user_label":"We are on it","raw_agent_label":"Investigating",
            "raw_end_user_label":"We are on it","description":"An agent is investigating","active":true,
            "created_at":"2024-01-02T03:04:05Z","updated_at":"2024-02-03T04:05:06Z"}]}
            """);

        var result = await tools.List(true, false, "open,pending", TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/custom_statuses", request.Path);
        var query = Uri.UnescapeDataString(request.Query);
        Assert.Contains("active=true", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("default=false", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status_categories=open,pending", query);
        // The lean envelope: summary rows carry the id→label decode set and nothing else.
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        var status = result.GetProperty("items")[0];
        Assert.Equal(31, status.GetProperty("id").GetInt64());
        Assert.Equal("open", status.GetProperty("status_category").GetString());
        Assert.Equal("Investigating", status.GetProperty("agent_label").GetString());
        Assert.True(status.GetProperty("active").GetBoolean());
        // Summary rows are allowlisted — end-user labels, raw_* variants, descriptions, and dates are
        // stripped; custom_statuses_get (or detail:'full') is the sink for them.
        Assert.False(status.TryGetProperty("end_user_label", out _));
        Assert.False(status.TryGetProperty("raw_agent_label", out _));
        Assert.False(status.TryGetProperty("description", out _));
        Assert.False(status.TryGetProperty("created_at", out _));
    }

    [Fact]
    public async Task List_Omits_Filters_When_Not_Provided()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"custom_statuses":[]}""");

        await tools.List(null, null, null, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/custom_statuses", request.Path);
        Assert.Equal(string.Empty, request.Query);
    }

    [Fact]
    public async Task List_Detail_Full_Returns_Unstripped_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"custom_statuses":[{"id":31,"url":"https://acme.zendesk.com/api/v2/custom_statuses/31.json",
            "status_category":"open","agent_label":"Investigating","raw_agent_label":"Investigating",
            "end_user_description":null,"created_at":"2024-01-02T03:04:05Z"}]}
            """);

        var result = await tools.List(detail: "full", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var status = result.GetProperty("items")[0];
        // Full rows keep everything the summary shape strips...
        Assert.Equal("Investigating", status.GetProperty("raw_agent_label").GetString());
        Assert.Equal("2024-01-02T03:04:05Z", status.GetProperty("created_at").GetString());
        // ...but are still the full VIEW: API self-links and null-valued fields are gone.
        Assert.False(status.TryGetProperty("url", out _));
        Assert.False(status.TryGetProperty("end_user_description", out _));
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
    public async Task Read_Requests_CustomStatus_By_Id_And_Returns_The_Full_View()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"custom_status":{"id":31,"url":"https://acme.zendesk.com/api/v2/custom_statuses/31.json",
            "status_category":"hold","agent_label":"Awaiting vendor","raw_agent_label":"Awaiting vendor",
            "end_user_label":null,"created_at":"2024-01-02T03:04:05Z","updated_at":"2024-02-03T04:05:06Z"}}
            """);

        var result = await tools.Read(31, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/custom_statuses/31", request.Path);
        // The full view keeps the server-assigned (spec read-only) fields the generated models would drop...
        Assert.Equal(31, result.GetProperty("id").GetInt64());
        Assert.Equal("2024-01-02T03:04:05Z", result.GetProperty("created_at").GetString());
        Assert.Equal("2024-02-03T04:05:06Z", result.GetProperty("updated_at").GetString());
        Assert.Equal("hold", result.GetProperty("status_category").GetString());
        Assert.Equal("Awaiting vendor", result.GetProperty("raw_agent_label").GetString());
        // ...minus API self-links and null-valued fields (absent = null/empty).
        Assert.False(result.TryGetProperty("url", out _));
        Assert.False(result.TryGetProperty("end_user_label", out _));
    }

    [Fact]
    public async Task Read_Throws_When_The_CustomStatus_Is_Missing()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("{}");

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(31, TestContext.Current.CancellationToken));

        Assert.Contains("'31'", exception.Message);
        Assert.Contains("not found", exception.Message);
    }
}