using ES.FX.Additions.Serilog.Lifetime;
using ES.FX.Hosting.Lifetime;
using ES.FX.Ignite.Hosting;
using ES.FX.Ignite.Serilog.Hosting;
using ES.FX.Ignite.Zendesk.Hosting;
using ES.FX.Zendesk.MCP.Host.Execution;
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
    var mcpServer = builder.AddZendeskMcpServer()
        .WithTools<ZendeskUserTools>()
        .WithTools<ZendeskTicketTools>()
        .WithTools<ZendeskFormTools>()
        .WithTools<ZendeskOrganizationTools>()
        .WithTools<ZendeskGroupTools>()
        .WithTools<ZendeskArticleTools>()
        .WithTools<ZendeskTicketFieldTools>()
        .WithTools<ZendeskMacroTools>()
        .WithTools<ZendeskAttachmentTools>()
        .WithTools<ZendeskSearchTools>()
        .WithTools<ZendeskViewTools>()
        .WithTools<ZendeskBrandTools>()
        .WithTools<ZendeskCustomStatusTools>()
        .WithTools<ZendeskJobStatusTools>()
        .WithTools<ZendeskTagTools>()
        .WithTools<ZendeskSuspendedTicketTools>();

    // Write tools are registered only when the configured baseline allows them: with a ReadOnly baseline the
    // per-request header can only tighten, so write tools could never execute — omitting them keeps the
    // agent's tool list truthful. ZendeskToolInvoker still enforces the effective mode on every call.
    if (!builder.GetMcpOptions().Execution.Mode.IsReadOnly())
        mcpServer
            .WithTools<ZendeskTicketWriteTools>()
            .WithTools<ZendeskUserWriteTools>()
            .WithTools<ZendeskOrganizationWriteTools>()
            .WithTools<ZendeskGroupWriteTools>()
            .WithTools<ZendeskFormWriteTools>()
            .WithTools<ZendeskTicketFieldWriteTools>()
            .WithTools<ZendeskMacroWriteTools>()
            .WithTools<ZendeskViewWriteTools>()
            .WithTools<ZendeskBrandWriteTools>()
            .WithTools<ZendeskCustomStatusWriteTools>()
            .WithTools<ZendeskSuspendedTicketWriteTools>()
            .WithTools<ZendeskUploadWriteTools>();

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