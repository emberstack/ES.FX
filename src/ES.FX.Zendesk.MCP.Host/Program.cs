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

    var mcpOptions = builder.GetMcpOptions();

    // Area gate (Mcp:Tools:Areas). Empty = all areas (backward compatible). An unknown/misspelled area throws
    // here with the list of valid areas — fail-closed, mirroring the execution-mode resolver. This composes with
    // the read-only baseline gate below via AND: read-only drops the write classes, the area gate drops classes
    // outside the configured areas.
    var areaGate = ZendeskToolAreaGate.FromConfiguration(mcpOptions.Tools.Areas, typeof(Program).Assembly);

    // MCP server (wired directly — MCP is a building block of this app, not a standalone package) + tools.
    var mcpServer = builder.AddZendeskMcpServer()
        .WithToolsInArea<ZendeskUserTools>(areaGate)
        .WithToolsInArea<ZendeskTicketTools>(areaGate)
        .WithToolsInArea<ZendeskFormTools>(areaGate)
        .WithToolsInArea<ZendeskOrganizationTools>(areaGate)
        .WithToolsInArea<ZendeskGroupTools>(areaGate)
        .WithToolsInArea<ZendeskArticleTools>(areaGate)
        .WithToolsInArea<ZendeskTicketFieldTools>(areaGate)
        .WithToolsInArea<ZendeskMacroTools>(areaGate)
        .WithToolsInArea<ZendeskAttachmentTools>(areaGate)
        .WithToolsInArea<ZendeskSearchTools>(areaGate)
        .WithToolsInArea<ZendeskViewTools>(areaGate)
        .WithToolsInArea<ZendeskBrandTools>(areaGate)
        .WithToolsInArea<ZendeskCustomStatusTools>(areaGate)
        .WithToolsInArea<ZendeskJobStatusTools>(areaGate)
        .WithToolsInArea<ZendeskTagTools>(areaGate)
        .WithToolsInArea<ZendeskSuspendedTicketTools>(areaGate)
        .WithToolsInArea<ZendeskSatisfactionRatingTools>(areaGate)
        .WithToolsInArea<ZendeskCommunityTools>(areaGate)
        .WithToolsInArea<ZendeskCustomObjectTools>(areaGate);

    // Write tools are registered only when the configured baseline allows them: with a ReadOnly baseline the
    // per-request header can only tighten, so write tools could never execute — omitting them keeps the
    // agent's tool list truthful. ZendeskToolInvoker still enforces the effective mode on every call. Each write
    // class is additionally subject to the area gate (AND), so Areas=tickets + ReadOnly=false registers only the
    // ticket write tools.
    if (!mcpOptions.Execution.Mode.IsReadOnly())
        mcpServer
            .WithToolsInArea<ZendeskTicketWriteTools>(areaGate)
            .WithToolsInArea<ZendeskUserWriteTools>(areaGate)
            .WithToolsInArea<ZendeskOrganizationWriteTools>(areaGate)
            .WithToolsInArea<ZendeskGroupWriteTools>(areaGate)
            .WithToolsInArea<ZendeskFormWriteTools>(areaGate)
            .WithToolsInArea<ZendeskTicketFieldWriteTools>(areaGate)
            .WithToolsInArea<ZendeskMacroWriteTools>(areaGate)
            .WithToolsInArea<ZendeskViewWriteTools>(areaGate)
            .WithToolsInArea<ZendeskBrandWriteTools>(areaGate)
            .WithToolsInArea<ZendeskCustomStatusWriteTools>(areaGate)
            .WithToolsInArea<ZendeskSuspendedTicketWriteTools>(areaGate)
            .WithToolsInArea<ZendeskUploadWriteTools>(areaGate);

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