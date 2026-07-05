using System.Net;
using System.Text.Json;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ES.FX.Zendesk.MCP.Host.Tools.Models;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskMacroWriteToolsTests
{
    private static (ZendeskMacroWriteTools Tools, ZendeskToolHarness Harness) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var harness = new ZendeskToolHarness();
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(m => m.EffectiveMode).Returns(mode);
        return (new ZendeskMacroWriteTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            executionMode.Object), harness);
    }

    [Fact]
    public async Task Create_Posts_Macro_Envelope_And_Returns_Lean_Confirmation()
    {
        var write = new ZendeskMacroWrite
        {
            Title = "Close and thank",
            Actions = [new ZendeskMacroActionWrite { Field = "status", Value = "solved" }]
        };
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"macro":{"id":31,"title":"Close and thank","active":true,"created_at":"2026-01-01T00:00:00Z",
             "url":"https://unit-test.zendesk.com/api/v2/macros/31.json",
             "actions":[{"field":"comment_value","value":["channel","Thanks!"]}]}}
            """);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/macros", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var macro = body.RootElement.GetProperty("macro");
        Assert.Equal("Close and thank", macro.GetProperty("title").GetString());
        var action = Assert.Single(macro.GetProperty("actions").EnumerateArray());
        Assert.Equal("status", action.GetProperty("field").GetString());
        Assert.Equal("solved", action.GetProperty("value").GetString());
        Assert.False(macro.TryGetProperty("description", out _));
        Assert.False(macro.TryGetProperty("active", out _));
        // The lean confirmation: identity + created_at only — the actions the agent just sent are NOT echoed
        // back a third time (macros_get is the verification sink), and API self-links are dropped.
        var element = Assert.IsType<JsonElement>(result);
        Assert.Equal(31, element.GetProperty("id").GetInt64());
        Assert.Equal("Close and thank", element.GetProperty("title").GetString());
        Assert.True(element.GetProperty("active").GetBoolean());
        Assert.Equal("2026-01-01T00:00:00Z", element.GetProperty("created_at").GetString());
        Assert.False(element.TryGetProperty("actions", out _));
        Assert.False(element.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Create_Maps_Every_Curated_Property_And_Echoes_The_Server_State_Of_Sent_Fields()
    {
        using var arrayValue = JsonDocument.Parse("""["vip","escalated"]""");
        var write = new ZendeskMacroWrite
        {
            Title = "Escalate",
            Description = "Escalates to tier 2",
            Active = true,
            Position = 5,
            Actions =
            [
                new ZendeskMacroActionWrite { Field = "status", Value = "open" },
                new ZendeskMacroActionWrite { Field = "current_tags", Value = arrayValue.RootElement }
            ]
        };
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"macro":{"id":32,"title":"Escalate","active":true,"description":"Escalates to tier 2",
             "position":5,"created_at":"2026-01-01T00:00:00Z","actions":[{"field":"status","value":"open"}]}}
            """);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(harness.Request.Body!);
        var macro = body.RootElement.GetProperty("macro");
        Assert.Equal("Escalate", macro.GetProperty("title").GetString());
        Assert.Equal("Escalates to tier 2", macro.GetProperty("description").GetString());
        Assert.True(macro.GetProperty("active").GetBoolean());
        Assert.Equal(5, macro.GetProperty("position").GetInt64());
        var actions = macro.GetProperty("actions");
        Assert.Equal("open", actions[0].GetProperty("value").GetString());
        Assert.Equal("current_tags", actions[1].GetProperty("field").GetString());
        Assert.Equal(JsonValueKind.Array, actions[1].GetProperty("value").ValueKind);
        Assert.Equal("vip", actions[1].GetProperty("value")[0].GetString());
        Assert.Equal("escalated", actions[1].GetProperty("value")[1].GetString());
        // Echo-of-change: the scalar fields the request set come back with their SERVER-state values.
        var element = Assert.IsType<JsonElement>(result);
        Assert.Equal("Escalates to tier 2", element.GetProperty("description").GetString());
        Assert.Equal(5, element.GetProperty("position").GetInt64());
        Assert.False(element.TryGetProperty("actions", out _));
    }

    [Fact]
    public async Task Update_Puts_Macro_Envelope_And_Returns_Lean_Confirmation()
    {
        var write = new ZendeskMacroWrite { Active = false };
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"macro":{"id":31,"title":"Reply","active":false,"updated_at":"2026-01-02T00:00:00Z",
             "actions":[{"field":"status","value":"solved"}]}}
            """);

        var result = await tools.Update(31, write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/macros/31", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var macro = body.RootElement.GetProperty("macro");
        Assert.False(macro.GetProperty("active").GetBoolean());
        Assert.False(macro.TryGetProperty("title", out _));
        Assert.False(macro.TryGetProperty("actions", out _));
        // The lean update confirmation: identity + updated_at; the untouched actions are not echoed back.
        var element = Assert.IsType<JsonElement>(result);
        Assert.Equal(31, element.GetProperty("id").GetInt64());
        Assert.False(element.GetProperty("active").GetBoolean());
        Assert.Equal("2026-01-02T00:00:00Z", element.GetProperty("updated_at").GetString());
        Assert.False(element.TryGetProperty("actions", out _));
    }

    [Fact]
    public async Task Update_Echoes_The_Server_State_Of_The_Fields_The_Request_Set()
    {
        var write = new ZendeskMacroWrite { Description = "Retired", Position = 2 };
        var (tools, harness) = Create();
        // The server may normalize/override what was sent — the echo-of-change must report ITS values.
        harness.EnqueueJson(
            """
            {"macro":{"id":31,"title":"Reply","active":true,"description":"Retired (archived)",
             "position":2,"updated_at":"2026-01-02T00:00:00Z"}}
            """);

        var result = await tools.Update(31, write, TestContext.Current.CancellationToken);

        var element = Assert.IsType<JsonElement>(result);
        Assert.Equal("Retired (archived)", element.GetProperty("description").GetString());
        Assert.Equal(2, element.GetProperty("position").GetInt64());
        Assert.Equal("2026-01-02T00:00:00Z", element.GetProperty("updated_at").GetString());
    }

    [Fact]
    public async Task Create_Throws_When_Zendesk_Returns_No_Macro()
    {
        var write = new ZendeskMacroWrite { Title = "Close and thank" };
        var (tools, harness) = Create();
        harness.EnqueueJson("{}");

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Create(write, TestContext.Current.CancellationToken));

        Assert.Contains("no macro", exception.Message);
    }

    [Fact]
    public async Task Delete_Sends_Delete_And_Acknowledges_With_The_Id()
    {
        var (tools, harness) = Create();
        harness.EnqueueStatus(HttpStatusCode.NoContent);

        var result = await tools.Delete(31, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/macros/31", request.Path);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("31", acknowledgement.Description);
        // The affected id is structured — the agent never has to parse it out of the description prose.
        Assert.Equal(31, acknowledgement.Id);
    }

    [Fact]
    public async Task Create_DryRun_Returns_DryRunResult_Without_Calling_Client()
    {
        var write = new ZendeskMacroWrite { Title = "Close and thank" };
        var (tools, harness) = Create(McpExecutionMode.DryRun);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("create macro 'Close and thank'", dryRun.Description);
        Assert.Same(write, dryRun.Request);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Delete_ReadOnly_Throws_Without_Calling_Client()
    {
        var (tools, harness) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete(31, TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        Assert.Empty(harness.Requests);
    }
}