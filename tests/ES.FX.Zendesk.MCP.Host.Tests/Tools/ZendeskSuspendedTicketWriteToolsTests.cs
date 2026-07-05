using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskSuspendedTicketWriteToolsTests
{
    private static (ZendeskSuspendedTicketWriteTools Tools, ZendeskToolHarness Harness) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var harness = new ZendeskToolHarness();
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(m => m.EffectiveMode).Returns(mode);
        return (new ZendeskSuspendedTicketWriteTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            executionMode.Object), harness);
    }

    [Fact]
    public async Task Recover_Puts_And_Returns_Ticket_Summary_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"ticket":[{"id":501,"url":"https://unit-test.zendesk.com/api/v2/tickets/501.json",
             "subject":"Recovered","status":"new","created_at":"2026-06-01T00:00:00Z",
             "custom_fields":[{"id":1,"value":"x"}]}]}
            """);

        var result = await tools.Recover(77, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/suspended_tickets/77/recover", request.Path);
        Assert.Null(request.Body);
        var element = Assert.IsType<JsonElement>(result);
        // The singular 'ticket' wire envelope is normalized to summary rows under the predictable 'tickets'.
        var ticket = element.GetProperty("tickets")[0];
        Assert.Equal(501, ticket.GetProperty("id").GetInt64());
        Assert.Equal("Recovered", ticket.GetProperty("subject").GetString());
        Assert.Equal("new", ticket.GetProperty("status").GetString());
        Assert.Equal("2026-06-01T00:00:00Z", ticket.GetProperty("created_at").GetString());
        // Summary rows are allowlisted — the token-heavy members and API self-links do not appear.
        Assert.False(ticket.TryGetProperty("custom_fields", out _));
        Assert.False(ticket.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Recover_Normalizes_The_Tickets_Envelope()
    {
        var (tools, harness) = Create();
        // Zendesk's docs and spec disagree on the envelope name (ticket vs tickets) — both must land under
        // the normalized 'tickets'.
        harness.EnqueueJson("""{"tickets":[{"id":501,"subject":"Recovered"}]}""");

        var result = await tools.Recover(77, TestContext.Current.CancellationToken);

        var element = Assert.IsType<JsonElement>(result);
        Assert.Equal(501, element.GetProperty("tickets")[0].GetProperty("id").GetInt64());
        Assert.Equal("Recovered", element.GetProperty("tickets")[0].GetProperty("subject").GetString());
    }

    [Fact]
    public async Task RecoverMany_Puts_Ids_And_Returns_Ticket_Summary_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"tickets":[{"id":501,"subject":"Recovered A"},{"id":502,"subject":"Recovered B"}],
             "suspended_tickets":[{"id":79,"cause":"Detected as spam","content":"the raw email"}]}
            """);

        var result = await tools.RecoverMany([77, 78, 79], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/suspended_tickets/recover_many", request.Path);
        Assert.Contains("ids=77,78,79", Uri.UnescapeDataString(request.Query));
        var element = Assert.IsType<JsonElement>(result);
        var tickets = element.GetProperty("tickets");
        Assert.Equal(2, tickets.GetArrayLength());
        Assert.Equal(501, tickets[0].GetProperty("id").GetInt64());
        Assert.Equal(502, tickets[1].GetProperty("id").GetInt64());
        // Recoveries that failed stay visible as suspended-ticket summary rows — raw email content stripped.
        var stillSuspended = element.GetProperty("suspended_tickets")[0];
        Assert.Equal(79, stillSuspended.GetProperty("id").GetInt64());
        Assert.Equal("Detected as spam", stillSuspended.GetProperty("cause").GetString());
        Assert.False(stillSuspended.TryGetProperty("content", out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task RecoverMany_Rejects_Invalid_Bulk_Counts_Without_Calling_Zendesk(int count)
    {
        var ids = Enumerable.Range(1, count).Select(i => (long)i).ToArray();
        var (tools, harness) = Create();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.RecoverMany(ids, TestContext.Current.CancellationToken));

        Assert.Equal("ids", exception.ParamName);
        Assert.Contains("between 1 and 100", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Delete_Sends_Delete_And_Returns_Acknowledgement_With_The_Id()
    {
        var (tools, harness) = Create();
        harness.EnqueueStatus(HttpStatusCode.NoContent);

        var result = await tools.Delete(77, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/suspended_tickets/77", request.Path);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete suspended ticket 77", acknowledgement.Description);
        // The affected id is structured — the agent never has to parse it back out of the prose.
        Assert.Equal(77, acknowledgement.Id);
    }

    [Fact]
    public async Task DeleteMany_Sends_Delete_With_Ids_And_Returns_Acknowledgement_With_The_Ids()
    {
        var (tools, harness) = Create();
        // QUIRK: plain 204 — this bulk delete is synchronous (acknowledgement, NOT a job status).
        harness.EnqueueStatus(HttpStatusCode.NoContent);

        var result = await tools.DeleteMany([77, 78], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/suspended_tickets/destroy_many", request.Path);
        Assert.Contains("ids=77,78", Uri.UnescapeDataString(request.Query));
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete 2 suspended tickets", acknowledgement.Description);
        Assert.Equal([77, 78], acknowledgement.Ids);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task DeleteMany_Rejects_Invalid_Bulk_Counts_Without_Calling_Zendesk(int count)
    {
        var ids = Enumerable.Range(1, count).Select(i => (long)i).ToArray();
        var (tools, harness) = Create();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.DeleteMany(ids, TestContext.Current.CancellationToken));

        Assert.Equal("ids", exception.ParamName);
        Assert.Contains("between 1 and 100", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task DryRun_Returns_The_Bulk_Digest_Without_Calling_Zendesk()
    {
        var ids = new long[] { 77, 78, 79 };
        var (tools, harness) = Create(McpExecutionMode.DryRun);

        var result = await tools.RecoverMany(ids, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("recover 3 suspended tickets", dryRun.Description);
        // Bulk dry-runs carry the digest, not a verbatim echo: per-item {index, id} rows.
        var digest = Assert.IsType<JsonObject>(dryRun.Request);
        Assert.Equal("recover", digest["action"]!.GetValue<string>());
        Assert.Equal("suspended_tickets", digest["target"]!.GetValue<string>());
        Assert.Equal(3, digest["count"]!.GetValue<int>());
        Assert.Equal(77, digest["items"]![0]!["id"]!.GetValue<long>());
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task ReadOnly_Rejects_Without_Calling_Zendesk()
    {
        var (tools, harness) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.DeleteMany([77, 78], TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        Assert.Empty(harness.Requests);
    }
}