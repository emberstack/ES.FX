using ES.FX.Additions.Serilog.Lifetime;
using ES.FX.Hosting.Lifetime;
using ES.FX.Ignite.Hosting;
using ES.FX.Ignite.Serilog.Hosting;
using ES.FX.Ignite.Zendesk.Hosting;
using ES.FX.Zendesk.MCP.Host.Hosting;
using ES.FX.Zendesk.MCP.Host.Tools;

return await ProgramEntry.CreateBuilder(args).UseSerilog().Build().RunAsync(async _ =>
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Logging.ClearProviders();
    builder.IgniteSerilog();

    // Ignite core: OpenTelemetry, health checks, resilient HttpClient, ASP.NET services.
    builder.Ignite();

    // Zendesk API client (Spark): config binding, authentication, typed HttpClient, live health check, tracing.
    builder.IgniteZendeskClient();

    // MCP server (wired directly — MCP is a building block of this app, not a standalone package) + tools.
    builder.AddZendeskMcpServer()
        .WithTools<ZendeskUserTools>()
        .WithTools<ZendeskTicketTools>()
        .WithTools<ZendeskFormTools>()
        .WithTools<ZendeskOrganizationTools>()
        .WithTools<ZendeskGroupTools>()
        .WithTools<ZendeskArticleTools>()
        .WithTools<ZendeskTicketFieldTools>()
        .WithTools<ZendeskMacroTools>()
        .WithTools<ZendeskAttachmentTools>();

    var app = builder.Build();

    app.Ignite();
    app.MapZendeskMcp();

    await app.RunAsync();
    return 0;
});

namespace ES.FX.Zendesk.MCP.Host
{
    /// <summary>
    ///     Program entry point marker type, exposed publicly for integration testing (WebApplicationFactory).
    /// </summary>
    public class Program;
}