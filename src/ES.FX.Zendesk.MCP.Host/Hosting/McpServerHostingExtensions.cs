using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Diagnostics;
using ES.FX.Zendesk.MCP.Host.Execution;
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
    ///     Registers the MCP server with the Streamable HTTP transport, execution-mode (read-only / dry-run)
    ///     support, and OpenTelemetry tracing/metrics.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <returns>The <see cref="IMcpServerBuilder" /> so tools can be registered by the caller.</returns>
    public static IMcpServerBuilder AddZendeskMcpServer(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<McpOptions>().BindConfiguration(McpOptions.SectionKey);

        // Execution-mode (read-only / dry-run) support, resolvable per request.
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IMcpExecutionModeAccessor, McpExecutionModeAccessor>();

        // Wire the MCP SDK telemetry into OpenTelemetry.
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource(McpTelemetry.ActivitySourceName))
            .WithMetrics(metrics => metrics.AddMeter(McpTelemetry.MeterName));

        // Materialize options now for registration-time transport configuration.
        var options = new McpOptions();
        builder.Configuration.GetSection(McpOptions.SectionKey).Bind(options);

        return builder.Services
            .AddMcpServer()
            .WithHttpTransport(transport => transport.Stateless = options.Stateless);
    }

    /// <summary>
    ///     Maps the MCP endpoints onto the application pipeline. Call after <c>app.Ignite()</c>.
    /// </summary>
    /// <param name="app">The <see cref="WebApplication" />.</param>
    /// <returns>The <see cref="WebApplication" /> for chaining.</returns>
    public static WebApplication MapZendeskMcp(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<McpOptions>>().Value;
        app.MapMcp(options.Endpoint);
        return app;
    }
}