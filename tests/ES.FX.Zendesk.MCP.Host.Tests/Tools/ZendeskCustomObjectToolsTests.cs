using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskCustomObjectToolsTests
{
    private static (ZendeskCustomObjectTools Tools, ZendeskToolHarness Harness) Create()
    {
        var harness = new ZendeskToolHarness();
        return (new ZendeskCustomObjectTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            new StaticOptionsMonitor<McpOptions>(new McpOptions())), harness);
    }

    [Fact]
    public async Task List_Requests_Object_Types_And_Returns_Lean_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"custom_objects":[{"key":"apartment","title":"Apartment","title_pluralized":"Apartments",
             "description":"Units","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-06-01T00:00:00Z",
             "raw_title":"Apartment","url":"https://unit-test.zendesk.com/api/v2/custom_objects/apartment.json"}]}
            """);

        var result = await tools.List(TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/custom_objects", harness.Request.Path);
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        var type = result.GetProperty("items")[0];
        Assert.Equal("apartment", type.GetProperty("key").GetString());
        Assert.Equal("Apartments", type.GetProperty("title_pluralized").GetString());
        // raw_* localization duplicates and self-links are stripped.
        Assert.False(type.TryGetProperty("raw_title", out _));
        Assert.False(type.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task RecordsList_Requests_Records_With_Cursor_And_Keeps_The_Fields_Map()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"custom_object_records":[{"id":"01HX","name":"Unit 4B","custom_object_key":"apartment",
             "external_id":"ext-4b","custom_object_fields":{"beds":2,"floor":4},
             "created_at":"2026-01-01T00:00:00Z","updated_at":"2026-06-01T00:00:00Z",
             "created_by_user_id":1,"updated_by_user_id":2,
             "photo":{"content_url":"https://cdn/x.png"},
             "url":"https://unit-test.zendesk.com/api/v2/custom_objects/apartment/records/01HX.json"}],
             "meta":{"has_more":true,"after_cursor":"NEXT"}}
            """);

        var result = await tools.RecordsList("apartment", cancellationToken: TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/custom_objects/apartment/records", request.Path);
        Assert.Contains("page%5Bsize%5D=25", request.Query);
        var record = result.GetProperty("items")[0];
        Assert.Equal("01HX", record.GetProperty("id").GetString());
        // custom_object_fields (the business data) is kept verbatim...
        Assert.Equal(2, record.GetProperty("custom_object_fields").GetProperty("beds").GetInt32());
        // ...while the photo and self-link are stripped.
        Assert.False(record.TryGetProperty("photo", out _));
        Assert.False(record.TryGetProperty("url", out _));
        // Cursor continuation surfaces on the envelope.
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal("NEXT", result.GetProperty("after_cursor").GetString());
    }

    [Fact]
    public async Task RecordsList_Passes_External_Ids_Filter()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"custom_object_records":[]}""");

        await tools.RecordsList("order", "ext-4b,ext-9c",
            cancellationToken: TestContext.Current.CancellationToken);

        // filter[external_ids] is percent-encoded on the wire.
        Assert.Contains("filter%5Bexternal_ids%5D=ext-4b", harness.Request.Query);
    }

    [Fact]
    public async Task RecordsSearch_Requests_The_Search_Endpoint_With_Query()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"custom_object_records":[{"id":"01HX","name":"Unit 4B"}]}""");

        var result = await tools.RecordsSearch("apartment", "4B",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/custom_objects/apartment/records/search", harness.Request.Path);
        Assert.Contains("query=4B", harness.Request.Query);
        Assert.Equal("Unit 4B", result.GetProperty("items")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task RecordsSearch_Rejects_A_Blank_Query_Without_Calling_Zendesk()
    {
        var (tools, harness) = Create();

        await Assert.ThrowsAsync<McpException>(() =>
            tools.RecordsSearch("apartment", "  ", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task RecordRead_Requests_The_Record_By_String_Id_And_Full_Views_It()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"custom_object_record":{"id":"01HX","name":"Unit 4B","custom_object_key":"apartment",
             "custom_object_fields":{"beds":2},"external_id":null,
             "url":"https://unit-test.zendesk.com/api/v2/custom_objects/apartment/records/01HX.json"}}
            """);

        var result = await tools.RecordRead("apartment", "01HX", TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/custom_objects/apartment/records/01HX", harness.Request.Path);
        Assert.Equal("Unit 4B", result.GetProperty("name").GetString());
        Assert.Equal(2, result.GetProperty("custom_object_fields").GetProperty("beds").GetInt32());
        // Full view drops null fields and API self-links.
        Assert.False(result.TryGetProperty("external_id", out _));
        Assert.False(result.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task RecordRead_Throws_When_The_Record_Is_Missing()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("{}");

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.RecordRead("apartment", "missing", TestContext.Current.CancellationToken));

        Assert.Contains("not found", exception.Message);
    }
}