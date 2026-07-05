using System.Diagnostics.CodeAnalysis;
using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Diagnostics;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tools;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;

namespace ES.FX.Zendesk.MCP.Host.Hosting;

/// <summary>
///     Hosting extensions that wire this application's MCP server (Streamable HTTP transport, execution-mode
///     support and OpenTelemetry). MCP is a building block of this app, so this lives in the app rather than a
///     standalone package.
/// </summary>
[PublicAPI]
public static class McpServerHostingExtensions
{
    /// <summary>
    ///     The server-level MCP <c>instructions</c> sent to clients at initialize: the lean response contract.
    ///     Per-tool descriptions still carry their own contract — this is reinforcement, not the sole carrier.
    /// </summary>
    internal const string ServerInstructions =
        "Zendesk MCP server — lean response contract: list/search tools return summary rows by default; pass " +
        "detail:'full' on the list tool, or call the record's *_get tool, for a complete record. An absent " +
        "field means null/empty, not unknown. Prefer the *_count tools to answer how-many questions, and " +
        "tickets_metrics_get for ticket timing metrics. A response's 'note' field carries dynamic conditions: " +
        "continuation (with has_more and after_cursor/next_page), truncation, and omitted data with the exact " +
        "re-call that retrieves it.";

    /// <summary>
    ///     Registers the MCP server with the Streamable HTTP transport, execution-mode (read-only / dry-run)
    ///     support, and OpenTelemetry tracing/metrics.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <returns>The <see cref="IMcpServerBuilder" /> so tools can be registered by the caller.</returns>
    public static IMcpServerBuilder AddZendeskMcpServer(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<McpOptions>().BindConfiguration(McpOptions.SectionKey).ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<McpOptions>, McpOptionsValidator>();

        // Execution-mode (read-only / dry-run) support, resolvable per request.
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IMcpExecutionModeAccessor, McpExecutionModeAccessor>();

        // Wire the MCP SDK telemetry into OpenTelemetry.
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource(McpTelemetry.ActivitySourceName))
            .WithMetrics(metrics => metrics.AddMeter(McpTelemetry.MeterName));

        // Materialize options now for registration-time transport configuration.
        var options = builder.GetMcpOptions();

        return builder.Services
            .AddMcpServer(serverOptions => serverOptions.ServerInstructions = ServerInstructions)
            .WithHttpTransport(transport => transport.Stateless = options.Stateless);
    }

    /// <summary>
    ///     Reads the <see cref="McpOptions" /> from configuration at registration time — for decisions that must
    ///     be made before the container is built (transport shape, conditional tool registration). Runtime code
    ///     should use the bound <see cref="IOptionsMonitor{TOptions}" /> instead.
    /// </summary>
    public static McpOptions GetMcpOptions(this IHostApplicationBuilder builder)
    {
        var options = new McpOptions();
        builder.Configuration.GetSection(McpOptions.SectionKey).Bind(options);
        return options;
    }

    /// <summary>
    ///     Registers a tool class only when the area gate admits its area, otherwise leaves the builder unchanged.
    ///     This composes with the read-only registration gate in <c>Program.cs</c> (which already omits the
    ///     <c>*WriteTools</c> classes under a read-only baseline) via AND: a class is registered only when it
    ///     passes both gates.
    /// </summary>
    /// <typeparam name="TTool">The <see cref="ModelContextProtocol.Server.McpServerToolTypeAttribute" /> tool class.</typeparam>
    /// <param name="builder">The MCP server builder.</param>
    /// <param name="gate">The area gate built from <c>Mcp:Tools:Areas</c>.</param>
    /// <returns>The builder, for chaining.</returns>
    public static IMcpServerBuilder WithToolsInArea<[DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
        TTool>(this IMcpServerBuilder builder, ZendeskToolAreaGate gate)
        => gate.Allows<TTool>() ? builder.WithTools<TTool>() : builder;

    /// <summary>
    ///     Maps the MCP endpoints onto the application pipeline. Call after <c>app.Ignite()</c>.
    /// </summary>
    /// <param name="app">The <see cref="WebApplication" />.</param>
    /// <returns>The <see cref="WebApplication" /> for chaining.</returns>
    public static WebApplication MapZendeskMcp(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<McpOptions>>().Value;

        // MCP transport spec: validate Origin on all incoming connections (DNS-rebinding protection). The
        // endpoint prefix is frozen to the exact route MapMcp is mapped at below.
        app.UseMiddleware<McpOriginValidationMiddleware>(options.Endpoint);

        app.MapMcp(options.Endpoint);
        return app;
    }
}