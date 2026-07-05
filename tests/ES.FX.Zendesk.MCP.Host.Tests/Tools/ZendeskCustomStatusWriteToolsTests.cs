using System.Net;
using System.Text.Json;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ES.FX.Zendesk.MCP.Host.Tools.Models;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskCustomStatusWriteToolsTests
{
    private static (ZendeskCustomStatusWriteTools Tools, ZendeskToolHarness Harness) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var harness = new ZendeskToolHarness();
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(m => m.EffectiveMode).Returns(mode);
        return (new ZendeskCustomStatusWriteTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            executionMode.Object), harness);
    }

    [Fact]
    public async Task Create_Posts_CustomStatus_Envelope_And_Returns_The_Lean_Confirmation()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"custom_status":{"id":11,"status_category":"hold","agent_label":"Awaiting vendor",
            "end_user_label":"On hold","raw_agent_label":"Awaiting vendor",
            "created_at":"2024-01-02T03:04:05Z","updated_at":"2024-01-02T03:04:05Z"}}
            """);
        var write = new ZendeskCustomStatusWrite
        {
            StatusCategory = "hold",
            AgentLabel = "Awaiting vendor",
            EndUserLabel = "On hold",
            Description = "Waiting on a third-party vendor",
            EndUserDescription = "We are waiting on a partner",
            Active = true
        };

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/custom_statuses", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var status = body.RootElement.GetProperty("custom_status");
        Assert.Equal("hold", status.GetProperty("status_category").GetString());
        Assert.Equal("Awaiting vendor", status.GetProperty("agent_label").GetString());
        Assert.Equal("On hold", status.GetProperty("end_user_label").GetString());
        Assert.Equal("Waiting on a third-party vendor", status.GetProperty("description").GetString());
        Assert.Equal("We are waiting on a partner", status.GetProperty("end_user_description").GetString());
        Assert.True(status.GetProperty("active").GetBoolean());
        // The lean confirmation: id + identity fields + created_at, nothing else (custom_statuses_get is the
        // sink for the labels and descriptions).
        var confirmation = Assert.IsType<JsonElement>(result);
        Assert.Equal(11, confirmation.GetProperty("id").GetInt64());
        Assert.Equal("hold", confirmation.GetProperty("status_category").GetString());
        Assert.Equal("Awaiting vendor", confirmation.GetProperty("agent_label").GetString());
        Assert.Equal("2024-01-02T03:04:05Z", confirmation.GetProperty("created_at").GetString());
        Assert.False(confirmation.TryGetProperty("end_user_label", out _));
        Assert.False(confirmation.TryGetProperty("raw_agent_label", out _));
        Assert.False(confirmation.TryGetProperty("updated_at", out _));
    }

    [Fact]
    public async Task Create_Passes_Unrecognized_StatusCategory_Through_Verbatim()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"custom_status":{"id":11}}""");
        var write = new ZendeskCustomStatusWrite { StatusCategory = "on_hold", AgentLabel = "Awaiting vendor" };

        await tools.Create(write, TestContext.Current.CancellationToken);

        // Unknown categories go on the wire untouched so Zendesk itself rejects them (legacy pass-through parity).
        using var body = JsonDocument.Parse(harness.Request.Body!);
        Assert.Equal("on_hold",
            body.RootElement.GetProperty("custom_status").GetProperty("status_category").GetString());
    }

    [Fact]
    public async Task Update_Puts_CustomStatus_Envelope_And_Echoes_Only_The_Requested_Fields()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"custom_status":{"id":11,"status_category":"hold","agent_label":"Awaiting supplier",
            "end_user_label":"On hold","active":false,"created_at":"2024-01-02T03:04:05Z",
            "updated_at":"2024-02-03T04:05:06Z"}}
            """);
        var write = new ZendeskCustomStatusWrite { AgentLabel = "Awaiting supplier", Active = false };

        var result = await tools.Update(11, write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/custom_statuses/11", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var status = body.RootElement.GetProperty("custom_status");
        Assert.Equal("Awaiting supplier", status.GetProperty("agent_label").GetString());
        Assert.False(status.GetProperty("active").GetBoolean());
        // Unset curated fields must be omitted from the wire (parity with the retired omit-null serializer).
        Assert.False(status.TryGetProperty("status_category", out _));
        Assert.False(status.TryGetProperty("end_user_label", out _));
        // The echo-of-change confirmation: {id, updated_at} plus the server-state values of exactly the
        // fields the request carried — nothing more.
        var confirmation = Assert.IsType<JsonElement>(result);
        Assert.Equal(11, confirmation.GetProperty("id").GetInt64());
        Assert.Equal("2024-02-03T04:05:06Z", confirmation.GetProperty("updated_at").GetString());
        Assert.Equal("Awaiting supplier", confirmation.GetProperty("agent_label").GetString());
        Assert.False(confirmation.GetProperty("active").GetBoolean());
        Assert.False(confirmation.TryGetProperty("status_category", out _));
        Assert.False(confirmation.TryGetProperty("end_user_label", out _));
        Assert.False(confirmation.TryGetProperty("created_at", out _));
    }

    [Fact]
    public async Task Update_Throws_When_The_Response_Has_No_CustomStatus()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("{}");

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Update(11, new ZendeskCustomStatusWrite { Active = false },
                TestContext.Current.CancellationToken));

        // The write may still have landed — the error must say so and name the verification tool.
        Assert.Contains("may still have been applied", exception.Message);
        Assert.Contains("custom_statuses_get", exception.Message);
    }

    [Fact]
    public async Task Delete_Sends_Delete_And_Returns_Acknowledgement_With_The_Structured_Id()
    {
        var (tools, harness) = Create();
        harness.EnqueueStatus(HttpStatusCode.NoContent);

        var result = await tools.Delete(11, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/custom_statuses/11", request.Path);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete custom ticket status 11", acknowledgement.Description);
        // The structured id — the agent chains it without parsing the prose.
        Assert.Equal(11, acknowledgement.Id);
    }

    [Fact]
    public async Task DryRun_Returns_DryRunResult_Without_Calling_Zendesk()
    {
        var write = new ZendeskCustomStatusWrite { StatusCategory = "hold", AgentLabel = "Awaiting vendor" };
        var (tools, harness) = Create(McpExecutionMode.DryRun);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("create custom ticket status 'Awaiting vendor'", dryRun.Description);
        Assert.Same(write, dryRun.Request);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task ReadOnly_Rejects_Without_Calling_Zendesk()
    {
        var (tools, harness) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Update(11, new ZendeskCustomStatusWrite { Active = false },
                TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        Assert.Empty(harness.Requests);
    }
}