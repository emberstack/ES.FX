using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskBrandToolsTests
{
    private static (ZendeskBrandTools Tools, ZendeskToolHarness Harness) Create()
    {
        var harness = new ZendeskToolHarness();
        return (new ZendeskBrandTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            new StaticOptionsMonitor<McpOptions>(new McpOptions())), harness);
    }

    [Fact]
    public async Task List_Sends_Cursor_Pagination_And_Returns_Summary_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"brands":[{"id":9,"url":"https://acme.zendesk.com/api/v2/brands/9.json","name":"Acme",
            "subdomain":"acme","active":true,"default":false,"has_help_center":true,
            "created_at":"2024-01-02T03:04:05Z","updated_at":"2024-02-03T04:05:06Z","ticket_form_ids":[7,8],
            "signature_template":"{{agent.signature}}",
            "logo":{"id":77,"file_name":"logo.png","content_url":"https://acme.zendesk.com/logo.png",
            "thumbnails":[{"id":78,"file_name":"logo_thumb.png"}]}}],
            "meta":{"has_more":true,"after_cursor":"cur2"}}
            """);

        var result = await tools.List(25, "cur1", TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/brands", request.Path);
        Assert.Contains("page%5Bsize%5D=25", request.Query);
        Assert.Contains("page%5Bafter%5D=cur1", request.Query);
        // The lean envelope: metadata first, continuation at the top level, summary rows in items.
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal("cur2", result.GetProperty("after_cursor").GetString());
        var brand = result.GetProperty("items")[0];
        Assert.Equal(9, brand.GetProperty("id").GetInt64());
        Assert.Equal("Acme", brand.GetProperty("name").GetString());
        Assert.Equal("acme", brand.GetProperty("subdomain").GetString());
        Assert.True(brand.GetProperty("active").GetBoolean());
        Assert.False(brand.GetProperty("default").GetBoolean());
        Assert.True(brand.GetProperty("has_help_center").GetBoolean());
        // Summary rows are allowlisted — the token-heavy members (logo with its per-size thumbnails, the
        // signature template) and fields outside the brand shape do not appear.
        Assert.False(brand.TryGetProperty("logo", out _));
        Assert.False(brand.TryGetProperty("signature_template", out _));
        Assert.False(brand.TryGetProperty("url", out _));
        Assert.False(brand.TryGetProperty("created_at", out _));
        Assert.False(brand.TryGetProperty("ticket_form_ids", out _));
    }

    [Fact]
    public async Task List_Omits_Pagination_When_Explicitly_Nulled()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"brands":[]}""");

        await tools.List(null, null, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/brands", request.Path);
        Assert.Equal(string.Empty, request.Query);
    }

    [Fact]
    public async Task List_Applies_The_Default_Page_Size()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"brands":[],"meta":{"has_more":false}}""");

        await tools.List(cancellationToken: TestContext.Current.CancellationToken);

        // The default page size is explicit on the wire — never left to Zendesk's server default of 100.
        Assert.Equal("/api/v2/brands", harness.Request.Path);
        Assert.Equal("?page%5Bsize%5D=25", harness.Request.Query);
    }

    [Fact]
    public async Task List_Detail_Full_Returns_Unstripped_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"brands":[{"id":9,"url":"https://acme.zendesk.com/api/v2/brands/9.json","name":"Acme",
            "subdomain":"acme","host_mapping":null,"signature_template":"{{agent.signature}}",
            "logo":{"id":77,"file_name":"logo.png"}}],"meta":{"has_more":false}}
            """);

        var result = await tools.List(detail: "full", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var brand = result.GetProperty("items")[0];
        // Full rows keep everything the summary shape strips...
        Assert.Equal("{{agent.signature}}", brand.GetProperty("signature_template").GetString());
        Assert.Equal("logo.png", brand.GetProperty("logo").GetProperty("file_name").GetString());
        // ...but are still the full VIEW: API self-links and null-valued fields are gone.
        Assert.False(brand.TryGetProperty("url", out _));
        Assert.False(brand.TryGetProperty("host_mapping", out _));
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
    public async Task Read_Requests_Brand_By_Id_And_Returns_The_Full_View_Without_Logo_Thumbnails()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"brand":{"id":9,"url":"https://acme.zendesk.com/api/v2/brands/9.json","name":"Acme",
            "subdomain":"acme","default":true,"host_mapping":null,"created_at":"2024-01-02T03:04:05Z",
            "logo":{"id":77,"file_name":"logo.png","content_url":"https://acme.zendesk.com/logo.png",
            "thumbnails":[{"id":78,"file_name":"logo_thumb.png"}]}}}
            """);

        var result = await tools.Read(9, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/brands/9", request.Path);
        // The full view keeps the server-assigned (spec read-only) fields the generated models would drop...
        Assert.Equal(9, result.GetProperty("id").GetInt64());
        Assert.Equal("2024-01-02T03:04:05Z", result.GetProperty("created_at").GetString());
        Assert.Equal("acme", result.GetProperty("subdomain").GetString());
        Assert.True(result.GetProperty("default").GetBoolean());
        // ...drops API self-links and null-valued fields (absent = null/empty)...
        Assert.False(result.TryGetProperty("url", out _));
        Assert.False(result.TryGetProperty("host_mapping", out _));
        // ...and strips only the logo's nested per-size thumbnails; the logo identity survives.
        var logo = result.GetProperty("logo");
        Assert.Equal("logo.png", logo.GetProperty("file_name").GetString());
        Assert.Equal("https://acme.zendesk.com/logo.png", logo.GetProperty("content_url").GetString());
        Assert.False(logo.TryGetProperty("thumbnails", out _));
    }

    [Fact]
    public async Task Read_Throws_When_The_Brand_Is_Missing()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("{}");

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(9, TestContext.Current.CancellationToken));

        Assert.Contains("'9'", exception.Message);
        Assert.Contains("not found", exception.Message);
    }
}