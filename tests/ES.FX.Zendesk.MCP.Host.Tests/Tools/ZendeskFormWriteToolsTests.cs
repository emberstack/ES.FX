using System.Net;
using System.Text.Json;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ES.FX.Zendesk.MCP.Host.Tools.Models;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskFormWriteToolsTests
{
    private static (ZendeskFormWriteTools Tools, ZendeskToolHarness Harness) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var harness = new ZendeskToolHarness();
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(m => m.EffectiveMode).Returns(mode);
        return (new ZendeskFormWriteTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            executionMode.Object), harness);
    }

    [Fact]
    public async Task Create_Posts_Ticket_Form_Envelope_And_Returns_A_Lean_Confirmation()
    {
        var write = new ZendeskTicketFormWrite { Name = "Billing" };
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"ticket_form":{"id":42,"url":"https://unit-test.zendesk.com/api/v2/ticket_forms/42.json",
             "name":"Billing","active":true,"created_at":"2026-01-01T00:00:00Z",
             "updated_at":"2026-01-01T00:00:00Z","ticket_field_ids":[1,2,3],"end_user_conditions":[]}}
            """);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/ticket_forms", request.Path);
        Assert.StartsWith("application/json", request.ContentType);
        using var body = JsonDocument.Parse(request.Body!);
        var form = body.RootElement.GetProperty("ticket_form");
        Assert.Equal("Billing", form.GetProperty("name").GetString());
        Assert.False(form.TryGetProperty("display_name", out _));
        Assert.False(form.TryGetProperty("ticket_field_ids", out _));
        var element = Assert.IsType<JsonElement>(result);
        // The lean confirmation ({id, name, active, created_at}) — the complete form is one forms_get away.
        Assert.Equal(42, element.GetProperty("id").GetInt64());
        Assert.Equal("Billing", element.GetProperty("name").GetString());
        Assert.True(element.GetProperty("active").GetBoolean());
        Assert.Equal("2026-01-01T00:00:00Z", element.GetProperty("created_at").GetString());
        Assert.False(element.TryGetProperty("url", out _));
        Assert.False(element.TryGetProperty("ticket_field_ids", out _));
        Assert.False(element.TryGetProperty("updated_at", out _));
    }

    [Fact]
    public async Task Create_Maps_Every_Curated_Property_And_Preserves_Field_Id_Order()
    {
        var write = new ZendeskTicketFormWrite
        {
            Name = "Billing",
            DisplayName = "Billing requests",
            Position = 2,
            Active = true,
            Default = false,
            EndUserVisible = true,
            InAllBrands = false,
            TicketFieldIds = [3, 1, 2]
        };
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"ticket_form":{"id":42,"name":"Billing"}}""");

        await tools.Create(write, TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(harness.Request.Body!);
        var form = body.RootElement.GetProperty("ticket_form");
        Assert.Equal("Billing", form.GetProperty("name").GetString());
        Assert.Equal("Billing requests", form.GetProperty("display_name").GetString());
        Assert.Equal(2, form.GetProperty("position").GetInt64());
        Assert.True(form.GetProperty("active").GetBoolean());
        Assert.False(form.GetProperty("default").GetBoolean());
        Assert.True(form.GetProperty("end_user_visible").GetBoolean());
        Assert.False(form.GetProperty("in_all_brands").GetBoolean());
        var fieldIds = form.GetProperty("ticket_field_ids");
        Assert.Equal(3, fieldIds.GetArrayLength());
        Assert.Equal(3, fieldIds[0].GetInt64());
        Assert.Equal(1, fieldIds[1].GetInt64());
        Assert.Equal(2, fieldIds[2].GetInt64());
    }

    [Fact]
    public async Task Update_Puts_Ticket_Form_Envelope_And_Omits_Unset_Properties()
    {
        var write = new ZendeskTicketFormWrite { Active = false };
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"ticket_form":{"id":42,"url":"https://unit-test.zendesk.com/api/v2/ticket_forms/42.json",
             "name":"Billing","active":false,"updated_at":"2026-03-01T00:00:00Z"}}
            """);

        var result = await tools.Update(42, write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/ticket_forms/42", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var form = body.RootElement.GetProperty("ticket_form");
        Assert.False(form.GetProperty("active").GetBoolean());
        Assert.False(form.TryGetProperty("name", out _));
        Assert.False(form.TryGetProperty("ticket_field_ids", out _));
        var element = Assert.IsType<JsonElement>(result);
        // Echo-of-change: {id, updated_at} plus the server-state values of exactly the fields the request set.
        Assert.Equal(42, element.GetProperty("id").GetInt64());
        Assert.Equal("2026-03-01T00:00:00Z", element.GetProperty("updated_at").GetString());
        Assert.False(element.GetProperty("active").GetBoolean());
        Assert.False(element.TryGetProperty("name", out _)); // not in the request → not echoed
        Assert.False(element.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Delete_Sends_Delete_And_Acknowledges()
    {
        var (tools, harness) = Create();
        harness.EnqueueStatus(HttpStatusCode.NoContent);

        var result = await tools.Delete(42, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/ticket_forms/42", request.Path);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("42", acknowledgement.Description);
        // The affected id rides structured on the acknowledgement — no prose parsing needed.
        Assert.Equal(42, acknowledgement.Id);
    }

    [Fact]
    public async Task Clone_Posts_To_Clone_Endpoint_And_Returns_A_Lean_Confirmation()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"ticket_form":{"id":43,"name":"Billing (copy)","active":true,"created_at":"2026-04-01T00:00:00Z",
             "ticket_field_ids":[3,1,2],"end_user_conditions":[{"parent_field_id":3,"value":"x"}],
             "agent_conditions":[{"parent_field_id":3,"value":"x"}]}}
            """);

        var result = await tools.Clone(42, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/ticket_forms/42/clone", request.Path);
        Assert.True(string.IsNullOrEmpty(request.Body));
        var element = Assert.IsType<JsonElement>(result);
        Assert.Equal(43, element.GetProperty("id").GetInt64());
        Assert.Equal("Billing (copy)", element.GetProperty("name").GetString());
        Assert.True(element.GetProperty("active").GetBoolean());
        Assert.Equal("2026-04-01T00:00:00Z", element.GetProperty("created_at").GetString());
        // The duplicated field list and condition trees are NOT echoed — forms_get is the sink for them.
        Assert.False(element.TryGetProperty("ticket_field_ids", out _));
        Assert.False(element.TryGetProperty("end_user_conditions", out _));
        Assert.False(element.TryGetProperty("agent_conditions", out _));
    }

    [Fact]
    public async Task Create_DryRun_Returns_DryRunResult_Without_Calling_Client()
    {
        var write = new ZendeskTicketFormWrite { Name = "Billing" };
        var (tools, harness) = Create(McpExecutionMode.DryRun);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("create ticket form 'Billing'", dryRun.Description);
        Assert.Same(write, dryRun.Request);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Delete_ReadOnly_Throws_Without_Calling_Client()
    {
        var (tools, harness) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete(42, TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        Assert.Empty(harness.Requests);
    }
}