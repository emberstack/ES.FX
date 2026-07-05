using System.ComponentModel;
using System.Text.Json;
using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.Support;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for Zendesk macros (canned responses). Namespaced <c>macros_*</c>.
/// </summary>
/// <remarks>
///     Macro responses are parsed as raw wire JSON rather than through the generated models: the published
///     spec types a macro action's <c>value</c> as a string, but the live API returns array values for
///     multi-value actions (most importantly <c>comment_value</c>, the canned reply text), which the typed
///     model would silently drop on re-serialization. List responses are then projected through
///     <see cref="ZendeskLean" /> into the uniform lean envelope — summary rows (actions stripped) by default,
///     complete records via <c>detail:'full'</c> or <c>macros_get</c>.
/// </remarks>
[McpServerToolType]
public sealed class ZendeskMacroTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IOptionsMonitor<McpOptions> mcpOptions)
{
    /// <summary>The uniform <c>detail</c> parameter description shared by the list tools.</summary>
    private const string DetailDescription =
        "Row detail: 'summary' (default, lean triage rows) | 'full' (complete records minus null fields and API " +
        "links).";

    /// <summary>Lists Zendesk macros.</summary>
    [McpServerTool(Name = "macros_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists Zendesk macros (canned responses + bulk actions agents apply to common issues: refunds, password " +
        "resets, escalations). Rows are lean summaries (id, title, active, description, usage stats when " +
        "present); actions (reply text, field changes) omitted — match by title/description then call macros_get " +
        "for exact reply text and side effects, or pass detail:'full'.")]
    public Task<JsonElement> List(
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100; Zendesk clamps higher to 100). Total in 'count'; " +
            "'has_more'/'next_page' drive paging.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var request = zendesk.Api.V2.Macros.ToGetRequestInformation(configuration =>
            {
                configuration.QueryParameters.Page = page;
                configuration.QueryParameters.PerPage = perPage;
            });
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "macros", page, parsedDetail,
                MaxResponseChars("macros_list"));
        });

    /// <summary>Lists only the macros usable by the current agent.</summary>
    [McpServerTool(Name = "macros_list_active", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Active macros usable by the current agent — pre-filtered macros_list excluding inactive/inaccessible " +
        "macros. Rows are lean summaries (actions omitted) — call macros_get for reply text and actions, or pass " +
        "detail:'full'.")]
    public Task<JsonElement> ListActive(
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100; Zendesk clamps higher to 100). Total in 'count'; " +
            "'has_more'/'next_page' drive paging.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // Escape hatch: the published spec models no paging on macros/active, but offset page/per_page is
            // officially supported — the endpoint doc specifies no pagination method, and
            // https://developer.zendesk.com/api-reference/introduction/pagination/ states such endpoints support
            // offset pagination (see the spec-anomaly ledger in src/ES.FX.Zendesk/OpenApi/README.md).
            var request = zendesk.Api.V2.Macros.Active.ToGetRequestInformation()
                .WithQuery("page", page)
                .WithQuery("per_page", perPage);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "macros", page, parsedDetail,
                MaxResponseChars("macros_list_active"));
        });

    /// <summary>Returns a single macro including its actions.</summary>
    [McpServerTool(Name = "macros_get", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Single macro including its actions — canned reply body plus field/tag/status changes it applies; detail " +
        "sink for macros_list rows. Null fields and API self-links omitted (absent field = null/empty).")]
    public Task<JsonElement> Read(
        [Description("Numeric macro id.")] long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var request = zendesk.Api.V2.Macros[id].ToGetRequestInformation();
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("macro", out var macro)
                ? ZendeskLean.EnsureWithinBudget(ZendeskLean.ToFullView(macro), "macros_get",
                    MaxResponseChars("macros_get"),
                    "Macro too large — its actions exceed the budget; there is no leaner single-record form.")
                : throw new McpException($"Zendesk macro '{id}' was not found.");
        });

    /// <summary>Resolves the response-size budget for a tool (see <see cref="McpToolsOptions.GetMaxResponseChars" />).</summary>
    private int MaxResponseChars(string toolName) => mcpOptions.CurrentValue.Tools.GetMaxResponseChars(toolName);
}