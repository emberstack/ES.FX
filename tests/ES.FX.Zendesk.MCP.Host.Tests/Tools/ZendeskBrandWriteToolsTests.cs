using System.Net;
using System.Text.Json;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ES.FX.Zendesk.MCP.Host.Tools.Models;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskBrandWriteToolsTests
{
    private static (ZendeskBrandWriteTools Tools, ZendeskToolHarness Harness) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var harness = new ZendeskToolHarness();
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(m => m.EffectiveMode).Returns(mode);
        return (new ZendeskBrandWriteTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            executionMode.Object), harness);
    }

    [Fact]
    public async Task Create_Posts_Brand_Envelope_And_Returns_The_Lean_Confirmation()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"brand":{"id":9,"url":"https://acme.zendesk.com/api/v2/brands/9.json","name":"Acme",
            "subdomain":"acme","default":true,"created_at":"2024-01-02T03:04:05Z","updated_at":"2024-01-02T03:04:05Z"}}
            """);
        var write = new ZendeskBrandWrite { Name = "Acme", Subdomain = "acme", Default = true };

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/brands", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var brand = body.RootElement.GetProperty("brand");
        Assert.Equal("Acme", brand.GetProperty("name").GetString());
        Assert.Equal("acme", brand.GetProperty("subdomain").GetString());
        Assert.True(brand.GetProperty("default").GetBoolean());
        // Unset curated fields must be omitted from the wire (parity with the retired omit-null serializer).
        Assert.False(brand.TryGetProperty("active", out _));
        Assert.False(brand.TryGetProperty("host_mapping", out _));
        // The lean confirmation: id + identity fields + created_at, nothing else (brands_get is the sink).
        var confirmation = Assert.IsType<JsonElement>(result);
        Assert.Equal(9, confirmation.GetProperty("id").GetInt64());
        Assert.Equal("Acme", confirmation.GetProperty("name").GetString());
        Assert.Equal("acme", confirmation.GetProperty("subdomain").GetString());
        Assert.Equal("2024-01-02T03:04:05Z", confirmation.GetProperty("created_at").GetString());
        Assert.False(confirmation.TryGetProperty("url", out _));
        Assert.False(confirmation.TryGetProperty("updated_at", out _));
        Assert.False(confirmation.TryGetProperty("default", out _));
    }

    [Fact]
    public async Task Update_Puts_Brand_Envelope_And_Echoes_Only_The_Requested_Fields()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"brand":{"id":9,"url":"https://acme.zendesk.com/api/v2/brands/9.json","name":"Acme EU",
            "subdomain":"acme","active":true,"default":true,"brand_url":"https://acme.eu",
            "host_mapping":"support.acme.eu","signature_template":"{{agent.signature}}",
            "created_at":"2024-01-02T03:04:05Z","updated_at":"2024-02-03T04:05:06Z"}}
            """);
        var write = new ZendeskBrandWrite
        {
            Name = "Acme EU",
            Active = false,
            BrandUrl = "https://acme.eu",
            HostMapping = "support.acme.eu",
            SignatureTemplate = "{{agent.signature}}"
        };

        var result = await tools.Update(9, write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/brands/9", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var brand = body.RootElement.GetProperty("brand");
        Assert.Equal("Acme EU", brand.GetProperty("name").GetString());
        Assert.False(brand.GetProperty("active").GetBoolean());
        Assert.Equal("https://acme.eu", brand.GetProperty("brand_url").GetString());
        Assert.Equal("support.acme.eu", brand.GetProperty("host_mapping").GetString());
        Assert.Equal("{{agent.signature}}", brand.GetProperty("signature_template").GetString());
        // The echo-of-change confirmation: {id, updated_at} plus the SERVER-STATE values of exactly the
        // fields the request carried — Zendesk returning active:true against the requested false reveals a
        // business-rule override without a follow-up brands_get.
        var confirmation = Assert.IsType<JsonElement>(result);
        Assert.Equal(9, confirmation.GetProperty("id").GetInt64());
        Assert.Equal("2024-02-03T04:05:06Z", confirmation.GetProperty("updated_at").GetString());
        Assert.Equal("Acme EU", confirmation.GetProperty("name").GetString());
        Assert.True(confirmation.GetProperty("active").GetBoolean());
        Assert.Equal("https://acme.eu", confirmation.GetProperty("brand_url").GetString());
        Assert.Equal("support.acme.eu", confirmation.GetProperty("host_mapping").GetString());
        Assert.Equal("{{agent.signature}}", confirmation.GetProperty("signature_template").GetString());
        // Fields NOT in the request are not echoed, and API self-links never survive.
        Assert.False(confirmation.TryGetProperty("subdomain", out _));
        Assert.False(confirmation.TryGetProperty("default", out _));
        Assert.False(confirmation.TryGetProperty("created_at", out _));
        Assert.False(confirmation.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Create_Throws_When_The_Response_Has_No_Brand()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("{}");
        var write = new ZendeskBrandWrite { Name = "Acme", Subdomain = "acme" };

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Create(write, TestContext.Current.CancellationToken));

        // The write may still have landed — the error must say so and name the verification tool.
        Assert.Contains("may still have been applied", exception.Message);
        Assert.Contains("brands_get", exception.Message);
    }

    [Fact]
    public async Task Delete_Sends_Delete_And_Returns_Acknowledgement_With_The_Structured_Id()
    {
        var (tools, harness) = Create();
        harness.EnqueueStatus(HttpStatusCode.NoContent);

        var result = await tools.Delete(9, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/brands/9", request.Path);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete brand 9", acknowledgement.Description);
        // The structured id — the agent chains it without parsing the prose.
        Assert.Equal(9, acknowledgement.Id);
    }

    [Fact]
    public async Task DryRun_Returns_DryRunResult_Without_Calling_Zendesk()
    {
        var write = new ZendeskBrandWrite { Name = "Acme", Subdomain = "acme" };
        var (tools, harness) = Create(McpExecutionMode.DryRun);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("create brand 'Acme'", dryRun.Description);
        Assert.Same(write, dryRun.Request);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task ReadOnly_Rejects_Without_Calling_Zendesk()
    {
        var (tools, harness) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete(9, TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        Assert.Empty(harness.Requests);
    }
}