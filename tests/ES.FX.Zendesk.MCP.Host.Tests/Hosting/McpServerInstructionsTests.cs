using ES.FX.Zendesk.MCP.Host.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tests.Hosting;

/// <summary>
///     Boots the real host to prove the server-level lean contract reaches clients: the MCP
///     <c>instructions</c> are set on <see cref="McpServerOptions" /> (sent at initialize), and a misconfigured
///     response budget fails startup instead of silently running unguarded.
/// </summary>
[Collection(HostEnvironmentCollection.Name)]
public class McpServerInstructionsTests
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

    [Fact]
    public void Server_Instructions_Carry_The_Lean_Response_Contract()
    {
        using var factory = CreateFactory();

        var options = factory.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;

        Assert.Equal(McpServerHostingExtensions.ServerInstructions, options.ServerInstructions);
        // The contract's load-bearing clauses, per the design: summary default, the two escalation paths,
        // the absence convention, the cheap-path steering, and where dynamic conditions live.
        Assert.Contains("summary rows by default", options.ServerInstructions);
        Assert.Contains("detail:'full'", options.ServerInstructions);
        Assert.Contains("*_get", options.ServerInstructions);
        Assert.Contains("absent field means null/empty, not unknown", options.ServerInstructions);
        Assert.Contains("*_count", options.ServerInstructions);
        Assert.Contains("tickets_metrics_get", options.ServerInstructions);
        Assert.Contains("'note'", options.ServerInstructions);
    }

    [Fact]
    public async Task A_Response_Budget_Below_The_Minimum_Fails_Startup()
    {
        // A plain host (not the full WebApplicationFactory boot — ProgramEntry's structured error handling
        // would swallow the failure there): AddZendeskMcpServer must wire binding + validator + ValidateOnStart
        // so the misconfiguration aborts startup instead of running unguarded.
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mcp:Tools:MaxResponseChars"] = "999"
        });
        builder.AddZendeskMcpServer();
        using var host = builder.Build();

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Mcp:Tools:MaxResponseChars", Flatten(exception));
        Assert.Contains("999", Flatten(exception));
    }

    /// <summary>All messages in the exception tree — startup failures arrive wrapped.</summary>
    private static string Flatten(Exception exception)
    {
        var messages = new List<string>();
        var pending = new Queue<Exception>([exception]);
        while (pending.TryDequeue(out var current))
        {
            messages.Add(current.Message);
            if (current is AggregateException aggregate)
                foreach (var inner in aggregate.InnerExceptions)
                    pending.Enqueue(inner);
            else if (current.InnerException is { } single) pending.Enqueue(single);
        }

        return string.Join(" | ", messages);
    }
}