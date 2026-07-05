using System.Net;
using System.Text.Json;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ES.FX.Zendesk.MCP.Host.Tools.Models;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskViewWriteToolsTests
{
    private static ZendeskViewWriteTools CreateTools(ZendeskToolHarness harness,
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(m => m.EffectiveMode).Returns(mode);
        return new ZendeskViewWriteTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            executionMode.Object);
    }

    [Fact]
    public async Task Create_Posts_View_Envelope_And_Returns_Lean_Confirmation()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"view":{"id":7,"title":"Escalations","active":true,"created_at":"2026-01-01T00:00:00Z",
             "url":"https://unit-test.zendesk.com/api/v2/views/7.json",
             "conditions":{"all":[{"field":"status","operator":"less_than","value":"solved"}],
              "any":[{"field":"group_id","operator":"is","value":"24000932"}]},
             "execution":{"columns":[{"id":"status","title":"Status"}],"group_by":"assignee"}}}
            """,
            HttpStatusCode.Created);
        var tools = CreateTools(harness);
        var write = new ZendeskViewWrite
        {
            Title = "Escalations",
            Active = true,
            All = [new ZendeskViewCondition { Field = "status", Operator = "less_than", Value = "solved" }],
            Any = [new ZendeskViewCondition { Field = "group_id", Operator = "is", Value = "24000932" }],
            Output = new ZendeskViewOutput
            {
                Columns = ["status", "requester", "assignee"],
                GroupBy = "assignee",
                GroupOrder = "desc",
                SortBy = "status",
                SortOrder = "desc"
            }
        };

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/views", request.Path);
        Assert.StartsWith("application/json", request.ContentType);
        var view = JsonDocument.Parse(request.Body!).RootElement.GetProperty("view");
        Assert.Equal("Escalations", view.GetProperty("title").GetString());
        Assert.True(view.GetProperty("active").GetBoolean());
        var all = view.GetProperty("all");
        Assert.Equal(1, all.GetArrayLength());
        Assert.Equal("status", all[0].GetProperty("field").GetString());
        Assert.Equal("less_than", all[0].GetProperty("operator").GetString());
        Assert.Equal("solved", all[0].GetProperty("value").GetString());
        var any = view.GetProperty("any");
        Assert.Equal("group_id", any[0].GetProperty("field").GetString());
        Assert.Equal("is", any[0].GetProperty("operator").GetString());
        Assert.Equal("24000932", any[0].GetProperty("value").GetString());
        var output = view.GetProperty("output");
        Assert.Equal(new[] { "status", "requester", "assignee" },
            output.GetProperty("columns").EnumerateArray().Select(column => column.GetString()).ToArray());
        Assert.Equal("assignee", output.GetProperty("group_by").GetString());
        Assert.Equal("desc", output.GetProperty("group_order").GetString());
        Assert.Equal("status", output.GetProperty("sort_by").GetString());
        Assert.Equal("desc", output.GetProperty("sort_order").GetString());
        // Unset fields are omitted from the payload (partial-write semantics).
        Assert.False(view.TryGetProperty("description", out _));

        // The lean confirmation: identity + created_at only — the conditions/execution the agent just sent
        // are NOT echoed back a third time (views_get is the verification sink), and API self-links are dropped.
        var element = Assert.IsType<JsonElement>(result);
        Assert.Equal(7, element.GetProperty("id").GetInt64());
        Assert.Equal("Escalations", element.GetProperty("title").GetString());
        Assert.True(element.GetProperty("active").GetBoolean());
        Assert.Equal("2026-01-01T00:00:00Z", element.GetProperty("created_at").GetString());
        Assert.False(element.TryGetProperty("conditions", out _));
        Assert.False(element.TryGetProperty("execution", out _));
        Assert.False(element.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Update_Puts_View_Envelope_To_View_Id_And_Returns_Lean_Confirmation()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"view":{"id":42,"title":"Escalations (EU)","active":true,"updated_at":"2026-01-02T00:00:00Z",
             "conditions":{"all":[{"field":"status","operator":"is","value":"open"}],"any":[]}}}
            """);
        var tools = CreateTools(harness);
        var write = new ZendeskViewWrite
        {
            Title = "Escalations (EU)",
            All = [new ZendeskViewCondition { Field = "status", Operator = "is", Value = "open" }]
        };

        var result = await tools.Update(42, write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/views/42", request.Path);
        Assert.StartsWith("application/json", request.ContentType);
        var view = JsonDocument.Parse(request.Body!).RootElement.GetProperty("view");
        Assert.Equal("Escalations (EU)", view.GetProperty("title").GetString());
        Assert.Equal("open", view.GetProperty("all")[0].GetProperty("value").GetString());
        Assert.False(view.TryGetProperty("any", out _));

        // The lean update confirmation: identity + updated_at; the conditions are not echoed back.
        var element = Assert.IsType<JsonElement>(result);
        Assert.Equal(42, element.GetProperty("id").GetInt64());
        Assert.Equal("Escalations (EU)", element.GetProperty("title").GetString());
        Assert.Equal("2026-01-02T00:00:00Z", element.GetProperty("updated_at").GetString());
        Assert.False(element.TryGetProperty("conditions", out _));
    }

    [Fact]
    public async Task Update_Echoes_The_Server_State_Of_The_Fields_The_Request_Set()
    {
        var harness = new ZendeskToolHarness();
        // The server may normalize/override what was sent — the echo-of-change must report ITS values.
        harness.EnqueueJson(
            """
            {"view":{"id":42,"title":"Escalations","active":true,"description":"EU escalations (normalized)",
             "updated_at":"2026-01-02T00:00:00Z"}}
            """);
        var tools = CreateTools(harness);
        var write = new ZendeskViewWrite { Description = "EU escalations" };

        var result = await tools.Update(42, write, TestContext.Current.CancellationToken);

        var element = Assert.IsType<JsonElement>(result);
        Assert.Equal("EU escalations (normalized)", element.GetProperty("description").GetString());
        Assert.Equal("2026-01-02T00:00:00Z", element.GetProperty("updated_at").GetString());
    }

    [Fact]
    public async Task Create_Throws_When_Zendesk_Returns_No_View()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("{}");
        var tools = CreateTools(harness);
        var write = new ZendeskViewWrite { Title = "Escalations" };

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Create(write, TestContext.Current.CancellationToken));

        Assert.Contains("no view", exception.Message);
    }

    [Fact]
    public async Task Delete_Sends_Delete_And_Returns_Acknowledgement_With_The_Id()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueStatus(HttpStatusCode.NoContent);
        var tools = CreateTools(harness);

        var result = await tools.Delete(42, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/views/42", request.Path);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete view 42", acknowledgement.Description);
        // The affected id is structured — the agent never has to parse it out of the description prose.
        Assert.Equal(42, acknowledgement.Id);
    }

    [Fact]
    public async Task DryRun_Returns_DryRunResult_Without_Sending_Request()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness, McpExecutionMode.DryRun);
        var write = new ZendeskViewWrite { Title = "Escalations" };

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("create view 'Escalations'", dryRun.Description);
        Assert.Same(write, dryRun.Request);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task ReadOnly_Rejects_Without_Sending_Request()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness, McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete(42, TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        Assert.Empty(harness.Requests);
    }
}