using System.Net;
using System.Text.Json;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ES.FX.Zendesk.MCP.Host.Tools.Models;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskTicketFieldWriteToolsTests
{
    private static (ZendeskToolHarness Harness, ZendeskTicketFieldWriteTools Tools) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var harness = new ZendeskToolHarness();
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(m => m.EffectiveMode).Returns(mode);
        return (harness, new ZendeskTicketFieldWriteTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            executionMode.Object));
    }

    [Fact]
    public async Task Create_Posts_TicketField_Envelope_And_Returns_A_Lean_Confirmation()
    {
        var (harness, tools) = Create();
        harness.EnqueueJson(
            """
            {"ticket_field":{"id":900,"url":"https://unit-test.zendesk.com/api/v2/ticket_fields/900.json",
             "type":"tagger","title":"Severity","active":true,"created_at":"2026-01-01T00:00:00Z",
             "updated_at":"2026-01-01T00:00:00Z",
             "custom_field_options":[{"id":11,"name":"High","value":"severity_high"},
              {"id":12,"name":"Low","value":"severity_low"}]}}
            """,
            HttpStatusCode.Created);
        var field = new ZendeskTicketFieldWrite
        {
            Type = "tagger",
            Title = "Severity",
            Position = 3,
            CustomFieldOptions =
            [
                new ZendeskCustomFieldOptionWrite { Name = "High", Value = "severity_high", AllowSolving = true },
                new ZendeskCustomFieldOptionWrite { Name = "Low", Value = "severity_low" }
            ]
        };

        var result = await tools.Create(field, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/ticket_fields", request.Path);
        Assert.StartsWith("application/json", request.ContentType);
        using var body = JsonDocument.Parse(request.Body!);
        var ticketField = body.RootElement.GetProperty("ticket_field");
        Assert.Equal("tagger", ticketField.GetProperty("type").GetString());
        Assert.Equal("Severity", ticketField.GetProperty("title").GetString());
        Assert.Equal(3, ticketField.GetProperty("position").GetInt32());
        var options = ticketField.GetProperty("custom_field_options");
        // Array ordering matters: Zendesk takes it as the option display order — pin it.
        Assert.Equal(2, options.GetArrayLength());
        Assert.Equal("High", options[0].GetProperty("name").GetString());
        Assert.Equal("severity_high", options[0].GetProperty("value").GetString());
        Assert.True(options[0].GetProperty("allow_solving").GetBoolean());
        Assert.Equal("Low", options[1].GetProperty("name").GetString());
        Assert.Equal("severity_low", options[1].GetProperty("value").GetString());
        // No id on new options — an id would flip the upsert to an update.
        Assert.False(options[0].TryGetProperty("id", out _));
        var created = Assert.IsType<JsonElement>(result);
        // The lean confirmation: identity fields plus the requested options, now carrying their assigned ids.
        Assert.Equal(900, created.GetProperty("id").GetInt64());
        Assert.Equal("tagger", created.GetProperty("type").GetString());
        Assert.Equal("Severity", created.GetProperty("title").GetString());
        Assert.Equal("2026-01-01T00:00:00Z", created.GetProperty("created_at").GetString());
        Assert.Equal(11, created.GetProperty("custom_field_options")[0].GetProperty("id").GetInt64());
        // The full definition is NOT echoed — ticket_fields_get is one call away.
        Assert.False(created.TryGetProperty("url", out _));
        Assert.False(created.TryGetProperty("active", out _));
        Assert.False(created.TryGetProperty("updated_at", out _));
    }

    [Fact]
    public async Task Create_Caps_The_Echoed_Options_At_20_With_A_Marker()
    {
        var (harness, tools) = Create();
        var serverOptions = string.Join(',', Enumerable.Range(1, 25)
            .Select(i => $$"""{"id":{{i}},"name":"O{{i}}","value":"o{{i}}"}"""));
        harness.EnqueueJson(
            $$$"""
               {"ticket_field":{"id":900,"type":"tagger","title":"Country","created_at":"2026-01-01T00:00:00Z",
                "custom_field_options":[{{{serverOptions}}}]}}
               """,
            HttpStatusCode.Created);
        var field = new ZendeskTicketFieldWrite
        {
            Type = "tagger",
            Title = "Country",
            CustomFieldOptions = Enumerable.Range(1, 25)
                .Select(i => new ZendeskCustomFieldOptionWrite { Name = $"O{i}", Value = $"o{i}" }).ToArray()
        };

        var result = await tools.Create(field, TestContext.Current.CancellationToken);

        var created = Assert.IsType<JsonElement>(result);
        Assert.Equal(20, created.GetProperty("custom_field_options").GetArrayLength());
        Assert.Equal("showing 20 of 25 options — read the complete definition with ticket_fields_get",
            created.GetProperty("options_truncated").GetString());
    }

    [Fact]
    public async Task Update_Puts_Only_The_Assigned_Fields()
    {
        var (harness, tools) = Create();
        harness.EnqueueJson(
            """
            {"ticket_field":{"id":7,"url":"https://unit-test.zendesk.com/api/v2/ticket_fields/7.json",
             "type":"text","title":"Impact","active":true,"updated_at":"2026-03-01T00:00:00Z"}}
            """);
        var field = new ZendeskTicketFieldWrite { Title = "Impact" };

        var result = await tools.Update(7, field, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/ticket_fields/7", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var ticketField = body.RootElement.GetProperty("ticket_field");
        Assert.Equal("Impact", ticketField.GetProperty("title").GetString());
        // Omit-null parity with the retired client: unset properties never reach the wire (sending
        // custom_field_options unintentionally would wipe the field's option set).
        Assert.False(ticketField.TryGetProperty("type", out _));
        Assert.False(ticketField.TryGetProperty("custom_field_options", out _));
        var updated = Assert.IsType<JsonElement>(result);
        // Echo-of-change: {id, updated_at} plus the server-state values of exactly the fields the request set.
        Assert.Equal(7, updated.GetProperty("id").GetInt64());
        Assert.Equal("2026-03-01T00:00:00Z", updated.GetProperty("updated_at").GetString());
        Assert.Equal("Impact", updated.GetProperty("title").GetString());
        Assert.False(updated.TryGetProperty("type", out _)); // not in the request → not echoed
        Assert.False(updated.TryGetProperty("active", out _));
        Assert.False(updated.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Delete_Sends_Delete_And_Acknowledges()
    {
        var (harness, tools) = Create();
        harness.EnqueueStatus(HttpStatusCode.NoContent);

        var result = await tools.Delete(7, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/ticket_fields/7", request.Path);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("7", acknowledgement.Description);
        // The affected id rides structured on the acknowledgement — no prose parsing needed.
        Assert.Equal(7, acknowledgement.Id);
    }

    [Fact]
    public async Task SetOption_Posts_Without_Id_To_Create()
    {
        var (harness, tools) = Create();
        harness.EnqueueJson("""{"custom_field_option":{"id":10,"name":"High","value":"high"}}""",
            HttpStatusCode.Created);
        var option = new ZendeskCustomFieldOptionWrite { Name = "High", Value = "high", AllowSolving = true };

        var result = await tools.SetOption(7, option, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/ticket_fields/7/options", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var sent = body.RootElement.GetProperty("custom_field_option");
        Assert.Equal("High", sent.GetProperty("name").GetString());
        Assert.Equal("high", sent.GetProperty("value").GetString());
        Assert.True(sent.GetProperty("allow_solving").GetBoolean());
        // Upsert contract: no id in the body means Zendesk creates a new option.
        Assert.False(sent.TryGetProperty("id", out _));
        var created = Assert.IsType<JsonElement>(result);
        Assert.Equal(10, created.GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task SetOption_Includes_Id_To_Update()
    {
        var (harness, tools) = Create();
        harness.EnqueueJson("""{"custom_field_option":{"id":10,"name":"Higher","value":"high"}}""");
        var option = new ZendeskCustomFieldOptionWrite { Id = 10, Name = "Higher", Value = "high" };

        _ = await tools.SetOption(7, option, TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(harness.Request.Body!);
        var sent = body.RootElement.GetProperty("custom_field_option");
        // Upsert contract: the id in the body is what turns the POST into an update of option 10.
        Assert.Equal(10, sent.GetProperty("id").GetInt64());
        Assert.Equal("Higher", sent.GetProperty("name").GetString());
    }

    [Fact]
    public async Task DeleteOption_Sends_Delete_And_Acknowledges()
    {
        var (harness, tools) = Create();
        harness.EnqueueStatus(HttpStatusCode.NoContent);

        var result = await tools.DeleteOption(7, 99, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/ticket_fields/7/options/99", request.Path);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("99", acknowledgement.Description);
        // The affected id rides structured on the acknowledgement — no prose parsing needed.
        Assert.Equal(99, acknowledgement.Id);
    }

    [Fact]
    public async Task Update_DryRun_Returns_DryRunResult_Without_Calling_Zendesk()
    {
        var (harness, tools) = Create(McpExecutionMode.DryRun);
        var field = new ZendeskTicketFieldWrite { Title = "Impact" };

        var result = await tools.Update(7, field, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("update ticket field 7", dryRun.Description);
        // The echo carries the target id alongside the payload so agents can machine-inspect the plan.
        var request = Assert.IsAssignableFrom<object>(dryRun.Request);
        Assert.Equal(7L, request.GetType().GetProperty("id")!.GetValue(request));
        Assert.Same(field, request.GetType().GetProperty("field")!.GetValue(request));
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Delete_ReadOnly_Throws_Without_Calling_Zendesk()
    {
        var (harness, tools) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete(7, TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        Assert.Empty(harness.Requests);
    }
}