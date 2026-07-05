using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskSuspendedTicketToolsTests
{
    private static (ZendeskSuspendedTicketTools Tools, ZendeskToolHarness Harness) Create()
    {
        var harness = new ZendeskToolHarness();
        return (new ZendeskSuspendedTicketTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            new StaticOptionsMonitor<McpOptions>(new McpOptions())), harness);
    }

    [Fact]
    public async Task List_Sends_Cursor_Pagination_And_Returns_Summary_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"suspended_tickets":[{"id":77,"url":"https://unit-test.zendesk.com/api/v2/suspended_tickets/77.json",
             "cause":"Detected as spam","subject":"Help","content":"Received: from mail.example.com ... the whole raw email",
             "author":{"name":"Spammy","email":"spam@example.com"},"brand_id":9,"ticket_id":null,
             "created_at":"2026-05-01T00:00:00Z"}],
             "meta":{"has_more":false}}
            """);

        var result = await tools.List(25, "cur1", TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/suspended_tickets", request.Path);
        Assert.Contains("page%5Bsize%5D=25", request.Query);
        Assert.Contains("page%5Bafter%5D=cur1", request.Query);
        // The lean envelope: metadata first, allowlisted summary rows in 'items'.
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.False(result.GetProperty("has_more").GetBoolean());
        var ticket = result.GetProperty("items")[0];
        Assert.Equal(77, ticket.GetProperty("id").GetInt64());
        Assert.Equal("Detected as spam", ticket.GetProperty("cause").GetString());
        Assert.Equal("2026-05-01T00:00:00Z", ticket.GetProperty("created_at").GetString());
        Assert.Equal("spam@example.com", ticket.GetProperty("author").GetProperty("email").GetString());
        Assert.Equal(9, ticket.GetProperty("brand_id").GetInt64());
        // The raw inbound email is STRIPPED from summary rows — suspended_tickets_get is the sink for it.
        Assert.False(ticket.TryGetProperty("content", out _));
        Assert.False(ticket.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task List_Applies_The_Default_Page_Size()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"suspended_tickets":[],"meta":{"has_more":false}}""");

        await tools.List(cancellationToken: TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/suspended_tickets", request.Path);
        // The default page size is explicit on the wire — never left to Zendesk's server default.
        Assert.Equal("?page%5Bsize%5D=25", request.Query);
    }

    [Fact]
    public async Task List_Detail_Full_Returns_Unstripped_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"suspended_tickets":[{"id":77,"url":"https://unit-test.zendesk.com/api/v2/suspended_tickets/77.json",
             "cause":"Detected as spam","content":"the whole raw email","ticket_id":null}],
             "meta":{"has_more":false}}
            """);

        var result = await tools.List(detail: "full", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var ticket = result.GetProperty("items")[0];
        Assert.Equal("the whole raw email", ticket.GetProperty("content").GetString()); // the complete record...
        Assert.False(ticket.TryGetProperty("url", out _)); // ...minus API self-links
        Assert.False(ticket.TryGetProperty("ticket_id", out _)); // ...and null-valued fields
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
    public async Task Read_Requests_SuspendedTicket_By_Id_And_Returns_The_Full_View()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"suspended_ticket":{"id":77,"url":"https://unit-test.zendesk.com/api/v2/suspended_tickets/77.json",
             "cause":"Detected as spam","created_at":"2026-05-01T00:00:00Z","ticket_id":null,
             "content":"the raw email","author":{"name":"Spammy"}}}
            """);

        var result = await tools.Read(77, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/suspended_tickets/77", request.Path);
        // The full-detail sink: the unwrapped record keeps the server-assigned (spec read-only) fields and the
        // raw email content...
        Assert.Equal(77, result.GetProperty("id").GetInt64());
        Assert.Equal("Detected as spam", result.GetProperty("cause").GetString());
        Assert.Equal("2026-05-01T00:00:00Z", result.GetProperty("created_at").GetString());
        Assert.Equal("the raw email", result.GetProperty("content").GetString());
        Assert.Equal("Spammy", result.GetProperty("author").GetProperty("name").GetString());
        // ...while the full view drops API self-links and null-valued fields (absent = null/empty).
        Assert.False(result.TryGetProperty("url", out _));
        Assert.False(result.TryGetProperty("ticket_id", out _));
    }

    [Fact]
    public async Task Read_Caps_The_Content_With_The_Recall_Marker()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"suspended_ticket":{"id":77,"content":"0123456789"}}""");

        var result = await tools.Read(77, TestContext.Current.CancellationToken, 4);

        // The marker names the exact re-call that returns the untruncated raw email.
        Assert.Equal(
            "0123…[truncated 6 chars — re-call with maxContentChars:0 (0 = no limit) for the full content]",
            result.GetProperty("content").GetString());
    }

    [Fact]
    public async Task Read_MaxContentChars_Zero_Disables_The_Cap()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"suspended_ticket":{"id":77,"content":"0123456789"}}""");

        var result = await tools.Read(77, TestContext.Current.CancellationToken, 0);

        Assert.Equal("0123456789", result.GetProperty("content").GetString());
    }

    [Fact]
    public async Task Read_Rejects_A_Negative_MaxContentChars_Without_Calling_Zendesk()
    {
        var (tools, harness) = Create();

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(77, TestContext.Current.CancellationToken, -1));

        Assert.Contains("maxContentChars", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Read_Throws_When_The_Suspended_Ticket_Is_Missing()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("{}");

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(77, TestContext.Current.CancellationToken));

        Assert.Contains("'77'", exception.Message);
        Assert.Contains("not found", exception.Message);
    }
}