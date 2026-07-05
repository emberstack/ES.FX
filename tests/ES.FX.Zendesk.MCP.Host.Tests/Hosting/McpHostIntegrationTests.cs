using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using ES.FX.Zendesk.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Hosting;

/// <summary>
///     Boots the REAL host (Program.cs) via <see cref="WebApplicationFactory{TEntryPoint}" /> to cover behavior
///     unit tests cannot: tool registration drift, conditional write-tool registration, the MCP endpoint mapping,
///     Origin validation, and the execution-mode header travelling through the request pipeline.
/// </summary>
[Collection(HostEnvironmentCollection.Name)]
public class McpHostIntegrationTests
{
    private static WebApplicationFactory<Program> CreateFactory(Dictionary<string, string?>? settings = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Never "Development": dev machines keep real secrets in appsettings.Development.json.
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var merged = new Dictionary<string, string?>
                {
                    ["Ignite:Zendesk:Subdomain"] = "unit-tests",
                    ["Ignite:Zendesk:OAuth:ClientId"] = "unit-tests-client",
                    ["Ignite:Zendesk:OAuth:ClientSecret"] = "unit-tests-secret"
                };
                foreach (var setting in settings ?? []) merged[setting.Key] = setting.Value;
                configuration.AddInMemoryCollection(merged);
            });
        });

    /// <summary>Every tool declared in the host assembly, keyed by MCP tool name.</summary>
    private static Dictionary<string, McpServerToolAttribute> DeclaredTools()
    {
        var attributes = typeof(Program).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>())
            .Where(attribute => attribute is not null)
            .Cast<McpServerToolAttribute>()
            .ToList();

        Assert.All(attributes, attribute => Assert.False(string.IsNullOrWhiteSpace(attribute.Name),
            "Every [McpServerTool] must set an explicit snake_case Name."));
        return attributes.ToDictionary(attribute => attribute.Name!, attribute => attribute);
    }

    private static HashSet<string> RegisteredTools(WebApplicationFactory<Program> factory) =>
        factory.Services.GetServices<McpServerTool>().Select(tool => tool.ProtocolTool.Name).ToHashSet();

    private static HttpRequestMessage McpRequest(string body, string path = "/")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return request;
    }

    [Fact]
    public void Every_Declared_Tool_Is_Registered()
    {
        // Drift guard: a new [McpServerToolType] class that is not wired into Program.cs (WithTools<T>) fails here.
        using var factory = CreateFactory();

        var declared = DeclaredTools().Keys.ToHashSet();
        var registered = RegisteredTools(factory);

        Assert.Empty(declared.Except(registered));
        Assert.Empty(registered.Except(declared));
    }

    [Fact]
    public void ReadOnly_Baseline_Registers_Only_Read_Tools()
    {
        // With a ReadOnly baseline the header can only tighten, so write tools could never execute — they
        // must not even be listed. The baseline is read at REGISTRATION time in Program.cs, which runs before
        // WebApplicationFactory can inject its configuration, so the environment-variable provider (read by
        // WebApplication.CreateBuilder itself) is the only seam that reaches it. Tests within a class run
        // sequentially, so the process-wide variable cannot leak into the sibling factory tests.
        Environment.SetEnvironmentVariable("Mcp__Execution__Mode", "ReadOnly");
        try
        {
            using var factory = CreateFactory();

            var declared = DeclaredTools();
            var registered = RegisteredTools(factory);
            var declaredReadOnly = declared.Where(tool => tool.Value.ReadOnly).Select(tool => tool.Key).ToHashSet();

            Assert.Empty(declaredReadOnly.Except(registered));
            Assert.Empty(registered.Except(declaredReadOnly));
        }
        finally
        {
            Environment.SetEnvironmentVariable("Mcp__Execution__Mode", null);
        }
    }

    [Fact]
    public void DryRun_Baseline_Still_Registers_All_Write_Tools()
    {
        // The registration gate in Program.cs is !IsReadOnly, NOT == Default: under a DryRun baseline every
        // write tool stays listed (calls are simulated by the invoker instead of hidden from the agent). Same
        // env-var seam and cleanup discipline as the ReadOnly baseline test above. The value must be the enum
        // name ("DryRun") — the configuration binder does not understand the header-style "dry-run" spelling.
        Environment.SetEnvironmentVariable("Mcp__Execution__Mode", "DryRun");
        try
        {
            using var factory = CreateFactory();

            var declared = DeclaredTools();
            var registered = RegisteredTools(factory);

            Assert.Empty(declared.Keys.Except(registered));
            Assert.Empty(registered.Except(declared.Keys));

            // Every one of the 12 *WriteTools classes contributes its full tool set.
            var writeToolTypes = typeof(Program).Assembly.GetTypes()
                .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null &&
                               type.Name.EndsWith("WriteTools", StringComparison.Ordinal))
                .ToList();
            Assert.Equal(12, writeToolTypes.Count);
            Assert.All(writeToolTypes, type => Assert.All(type
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                                BindingFlags.DeclaredOnly)
                    .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
                    .Where(name => name is not null),
                name => Assert.Contains(name!, registered)));
        }
        finally
        {
            Environment.SetEnvironmentVariable("Mcp__Execution__Mode", null);
        }
    }

    [Fact]
    public async Task Mcp_Endpoint_Accepts_Initialize()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.SendAsync(McpRequest(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"integration-tests","version":"1.0.0"}}}"""),
            TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Mcp_Endpoint_Rejects_Unknown_Browser_Origin()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var request = McpRequest(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"integration-tests","version":"1.0.0"}}}""");
        request.Headers.Add("Origin", "https://evil.example");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Execution_Mode_Header_Tightens_To_ReadOnly_Through_The_Pipeline()
    {
        // End-to-end: a write tool invoked with the tighten header must be rejected by the invoker guard —
        // and never reach the Zendesk client (which would hit the network).
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var request = McpRequest(
            """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"tickets_delete","arguments":{"id":1}}}""");
        request.Headers.Add("X-Mcp-Execution-Mode", "read-only");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("read-only", body);
        Assert.Contains("NOT performed", body);
    }

    [Fact]
    public async Task Execution_Mode_Header_DryRun_Simulates_The_Write_Through_The_Pipeline()
    {
        // End-to-end: a write tool invoked with the dry-run tighten header must come back as an explicit
        // dry_run result (executed:false) — and never touch the Zendesk client. The real client is replaced
        // with a strict mock, so ANY member access on it would surface as an error result instead.
        var zendeskClient = new Mock<IZendeskClient>(MockBehavior.Strict);
        using var factory = CreateFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton(zendeskClient.Object)));
        using var client = factory.CreateClient();

        var request = McpRequest(
            """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"tickets_delete","arguments":{"id":1}}}""");
        request.Headers.Add("X-Mcp-Execution-Mode", "dry-run");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("dry_run", body);
        Assert.Contains("no changes were made", body);
        Assert.Contains("soft-delete ticket 1", body);
        // The dry-run payload is a JSON string inside a text content block, so its quotes arrive escaped
        // (as " or \") — normalize them before asserting on the executed flag.
        var normalized = body.Replace("\\u0022", "\"").Replace("\\\"", "\"");
        Assert.Contains("\"executed\":false", normalized);
        zendeskClient.VerifyNoOtherCalls();
    }
}
