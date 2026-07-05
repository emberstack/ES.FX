using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskFormToolsTests
{
    private static (ZendeskFormTools Tools, ZendeskToolHarness Harness) Create()
    {
        var harness = new ZendeskToolHarness();
        return (new ZendeskFormTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            new StaticOptionsMonitor<McpOptions>(new McpOptions())), harness);
    }

    [Fact]
    public async Task Search_Requests_Ticket_Forms_And_Returns_Summary_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"ticket_forms":[{"id":7,"url":"https://unit-test.zendesk.com/api/v2/ticket_forms/7.json",
             "name":"Default","display_name":"Default form","active":true,"default":true,"position":1,
             "ticket_field_ids":[3,1,2],
             "end_user_conditions":[{"parent_field_id":3,"value":"x","child_fields":[{"id":9}]}],
             "agent_conditions":[{"parent_field_id":3,"value":"x","child_fields":[{"id":9}]}],
             "created_at":"2026-01-01T00:00:00Z","updated_at":"2026-02-01T00:00:00Z"}],
             "meta":{"has_more":false,"after_cursor":null}}
            """);

        var result = await tools.Search(cancellationToken: TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/ticket_forms", request.Path);
        // The default page size is explicit on the wire — never left to Zendesk's server default.
        Assert.Equal("?page%5Bsize%5D=25", request.Query);
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.False(result.GetProperty("has_more").GetBoolean());
        var form = result.GetProperty("items")[0];
        Assert.Equal(7, form.GetProperty("id").GetInt64());
        Assert.Equal("Default", form.GetProperty("name").GetString());
        Assert.True(form.GetProperty("active").GetBoolean());
        Assert.True(form.GetProperty("default").GetBoolean());
        Assert.Equal(1, form.GetProperty("position").GetInt32());
        // The ordered field ids survive on the summary row...
        var fieldIds = form.GetProperty("ticket_field_ids");
        Assert.Equal(3, fieldIds[0].GetInt64());
        Assert.Equal(1, fieldIds[1].GetInt64());
        Assert.Equal(2, fieldIds[2].GetInt64());
        // ...while the token-heavy condition trees and self-links are stripped (forms_get is the sink).
        Assert.False(form.TryGetProperty("end_user_conditions", out _));
        Assert.False(form.TryGetProperty("agent_conditions", out _));
        Assert.False(form.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Search_Passes_Cursor_Paging_Parameters_Through()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"ticket_forms":[],"meta":{"has_more":false}}""");

        await tools.Search(50, "cursor-1", TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/ticket_forms", request.Path);
        Assert.Contains("page%5Bsize%5D=50", request.Query);
        Assert.Contains("page%5Bafter%5D=cursor-1", request.Query);
    }

    [Fact]
    public async Task Search_Detail_Full_Returns_Unstripped_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"ticket_forms":[{"id":7,"url":"https://unit-test.zendesk.com/api/v2/ticket_forms/7.json",
             "name":"Default","display_name":null,
             "end_user_conditions":[{"parent_field_id":3,"value":"x"}]}],"meta":{"has_more":false}}
            """);

        var result = await tools.Search(detail: "full", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var form = result.GetProperty("items")[0];
        Assert.True(form.TryGetProperty("end_user_conditions", out _)); // the complete record...
        Assert.False(form.TryGetProperty("url", out _)); // ...minus API self-links
        Assert.False(form.TryGetProperty("display_name", out _)); // ...and null-valued fields
    }

    [Fact]
    public async Task Search_Rejects_An_Invalid_Detail_Without_Calling_Zendesk()
    {
        var (tools, harness) = Create();

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Search(detail: "everything", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("'summary'", exception.Message);
        Assert.Contains("'full'", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Read_Requests_Ticket_Form_By_Id_And_Returns_The_Full_View()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"ticket_form":{"id":7,"url":"https://unit-test.zendesk.com/api/v2/ticket_forms/7.json",
             "name":"Billing","display_name":null,"created_at":"2026-01-01T00:00:00Z","ticket_field_ids":[3,1,2],
             "end_user_conditions":[{"parent_field_id":3,"value":"x"}]}}
            """);

        var result = await tools.Read(7, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/ticket_forms/7", request.Path);
        // The envelope is unwrapped: the full-detail form, including the condition trees forms_list strips...
        Assert.Equal(7, result.GetProperty("id").GetInt64());
        Assert.Equal("Billing", result.GetProperty("name").GetString());
        Assert.Equal("2026-01-01T00:00:00Z", result.GetProperty("created_at").GetString());
        var fieldIds = result.GetProperty("ticket_field_ids");
        Assert.Equal(3, fieldIds[0].GetInt64());
        Assert.Equal(1, fieldIds[1].GetInt64());
        Assert.Equal(2, fieldIds[2].GetInt64());
        Assert.True(result.TryGetProperty("end_user_conditions", out _));
        // ...minus API self-links and null-valued fields.
        Assert.False(result.TryGetProperty("url", out _));
        Assert.False(result.TryGetProperty("display_name", out _));
    }

    [Fact]
    public async Task Read_Throws_When_The_Form_Is_Missing()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("{}");

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(7, TestContext.Current.CancellationToken));

        Assert.Contains("'7'", exception.Message);
        Assert.Contains("not found", exception.Message);
    }
}