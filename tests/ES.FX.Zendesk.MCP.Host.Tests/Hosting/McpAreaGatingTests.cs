using System.Reflection;
using ES.FX.Zendesk.MCP.Host.Tools;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tests.Hosting;

/// <summary>
///     Boots the REAL host (Program.cs) via <see cref="WebApplicationFactory{TEntryPoint}" /> to verify the
///     area gate (<c>Mcp:Tools:Areas</c>) drives which tool classes are registered, and how it composes with the
///     read-only execution baseline (AND).
/// </summary>
/// <remarks>
///     The area set — like the execution baseline — is read at REGISTRATION time in Program.cs, which runs
///     before <see cref="WebApplicationFactory{TEntryPoint}" /> can inject its own configuration, so the
///     environment-variable provider (read by <c>WebApplication.CreateBuilder</c> itself) is the only seam that
///     reaches it. Array elements use the <c>Mcp__Tools__Areas__{index}</c> convention. Tests within a class run
///     sequentially, so the process-wide variables cannot leak into sibling tests; each test restores them in a
///     finally block. The unknown-area fail-closed path is asserted precisely as a unit test in
///     <see cref="Tools.ZendeskToolAreaTests" /> (a registration-time throw is swallowed by ProgramEntry before
///     the factory can capture the host, so it is not observable here).
/// </remarks>
[Collection(HostEnvironmentCollection.Name)]
public class McpAreaGatingTests
{
    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Ignite:Zendesk:Subdomain"] = "unit-tests",
                    ["Ignite:Zendesk:OAuth:ClientId"] = "unit-tests-client",
                    ["Ignite:Zendesk:OAuth:ClientSecret"] = "unit-tests-secret"
                }));
        });

    private static HashSet<string> RegisteredTools(WebApplicationFactory<Program> factory) =>
        factory.Services.GetServices<McpServerTool>().Select(tool => tool.ProtocolTool.Name).ToHashSet();

    /// <summary>Every declared tool name paired with its ReadOnly flag and derived area.</summary>
    private static List<(string Name, bool ReadOnly, string Area)> DeclaredTools() =>
        typeof(Program).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>())
            .Where(attribute => attribute is not null && !string.IsNullOrWhiteSpace(attribute.Name))
            .Select(attribute => (attribute!.Name!, attribute.ReadOnly, ZendeskToolArea.OfToolName(attribute.Name!)))
            .ToList();

    [Fact]
    public void No_Areas_Configured_Registers_Every_Tool()
    {
        // Backward compatible: an absent Mcp:Tools:Areas registers the full surface.
        using var factory = CreateFactory();

        var declared = DeclaredTools().Select(tool => tool.Name).ToHashSet();
        var registered = RegisteredTools(factory);

        Assert.Empty(declared.Except(registered));
        Assert.Empty(registered.Except(declared));
    }

    [Fact]
    public void Areas_Tickets_Registers_Only_Ticket_Area_Tools()
    {
        SetAreas("tickets");
        try
        {
            using var factory = CreateFactory();

            var registered = RegisteredTools(factory);
            var ticketTools = DeclaredTools().Where(tool => tool.Area == "tickets").Select(tool => tool.Name)
                .ToHashSet();

            Assert.NotEmpty(registered);
            Assert.Empty(ticketTools.Except(registered));            // every ticket tool present
            Assert.Empty(registered.Except(ticketTools));            // and nothing outside the tickets area
            Assert.All(registered, name => Assert.Equal("tickets", ZendeskToolArea.OfToolName(name)));
            // search_count is a search-area tool and must NOT leak in with tickets.
            Assert.DoesNotContain("search_count", registered);
            // tickets_search_export lives on the ticket class and IS a ticket tool — it must be present.
            Assert.Contains("tickets_search_export", registered);
        }
        finally
        {
            ClearHostEnvironment();
        }
    }

    [Fact]
    public void Areas_Tickets_And_ReadOnly_Registers_Only_Ticket_Read_Tools()
    {
        // AND composition: the area gate narrows to the tickets area; the read-only baseline drops the write
        // classes. The intersection is exactly the ticket READ tools.
        SetAreas("tickets");
        Environment.SetEnvironmentVariable("Mcp__Execution__Mode", "ReadOnly");
        try
        {
            using var factory = CreateFactory();

            var registered = RegisteredTools(factory);
            var ticketReadTools = DeclaredTools()
                .Where(tool => tool is { ReadOnly: true, Area: "tickets" }).Select(tool => tool.Name).ToHashSet();

            Assert.NotEmpty(registered);
            Assert.Empty(ticketReadTools.Except(registered));
            Assert.Empty(registered.Except(ticketReadTools));
            // No write tools survived: a known ticket write must be absent.
            Assert.DoesNotContain("tickets_delete", registered);
            Assert.Contains("tickets_get", registered);
        }
        finally
        {
            ClearHostEnvironment();
        }
    }

    [Fact]
    public void Multiple_Areas_Register_The_Union_Of_Those_Areas()
    {
        SetAreas("tickets", "search");
        try
        {
            using var factory = CreateFactory();

            var registered = RegisteredTools(factory);
            var expected = DeclaredTools().Where(tool => tool.Area is "tickets" or "search").Select(tool => tool.Name)
                .ToHashSet();

            Assert.Empty(expected.Except(registered));
            Assert.Empty(registered.Except(expected));
            Assert.Contains("search_count", registered);
            Assert.Contains("tickets_get", registered);
            Assert.DoesNotContain("users_get", registered);
        }
        finally
        {
            ClearHostEnvironment();
        }
    }

    /// <summary>Maximum area indices this class ever sets — cleared in full so no index leaks between tests.</summary>
    private const int MaxAreaIndex = 4;

    /// <summary>Sets <c>Mcp__Tools__Areas__{index}</c> for each supplied area (positional).</summary>
    private static void SetAreas(params string[] areas)
    {
        for (var index = 0; index < areas.Length; index++)
            Environment.SetEnvironmentVariable($"Mcp__Tools__Areas__{index}", areas[index]);
    }

    /// <summary>
    ///     Clears every process-wide variable this class mutates: all area indices (a generous range, so a
    ///     multi-index test cannot leave a stale higher index behind) and the execution mode.
    /// </summary>
    private static void ClearHostEnvironment()
    {
        for (var index = 0; index <= MaxAreaIndex; index++)
            Environment.SetEnvironmentVariable($"Mcp__Tools__Areas__{index}", null);
        Environment.SetEnvironmentVariable("Mcp__Execution__Mode", null);
    }
}
