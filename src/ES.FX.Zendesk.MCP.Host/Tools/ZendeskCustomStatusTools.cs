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
///     MCP read tools for Zendesk custom ticket statuses. Namespaced <c>custom_statuses_*</c>.
/// </summary>
/// <remarks>
///     Requests are built through the generated request builders, but responses are returned as the raw wire
///     JSON (<see cref="ZendeskKiotaRequests.SendForJsonAsync" />) rather than round-tripped through the
///     generated models: the published spec marks server-assigned fields (custom status <c>id</c>,
///     <c>created_at</c>/<c>updated_at</c>, the raw label variants, ...) as read-only, so Kiota's serializer
///     would silently drop them from the tool result. List responses are then projected through
///     <see cref="ZendeskLean" /> into the uniform lean envelope — summary rows by default, complete records
///     via <c>detail:'full'</c> or <c>custom_statuses_get</c>.
/// </remarks>
[McpServerToolType]
public sealed class ZendeskCustomStatusTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IOptionsMonitor<McpOptions> mcpOptions)
{
    /// <summary>The uniform <c>detail</c> parameter description for the list tool.</summary>
    private const string DetailDescription =
        "Row detail: 'summary' (default, lean rows) | 'full' (complete records, null fields and API links omitted).";

    /// <summary>Lists Zendesk custom ticket statuses.</summary>
    [McpServerTool(Name = "custom_statuses_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Custom ticket statuses as lean summary rows (id, status_category, agent_label, active; end-user labels, " +
        "raw_* label variants, descriptions stripped); decodes the custom_status_id on tickets when custom " +
        "statuses enabled. detail:'full' for complete records; custom_statuses_get for one. Not paginated: full " +
        "list returned — small and account-stable, so fetch once per session and cache the id->label mapping. " +
        "Read-only.")]
    public Task<JsonElement> List(
        [Description("Filter: active (true) | inactive (false). Optional.")]
        bool? active = null,
        [Description("Filter: default (true) | non-default (false). Optional.")]
        bool? @default = null,
        [Description("Filter by comma-separated status categories: new|open|pending|hold|solved. Optional.")]
        string? statusCategories = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var request = zendesk.Api.V2.Custom_statuses.ToGetRequestInformation(cfg =>
            {
                cfg.QueryParameters.Active = active;
                cfg.QueryParameters.Default = @default;
                cfg.QueryParameters.StatusCategories = statusCategories;
            });
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            // The endpoint is unpaginated (no meta/next_page), so the offset envelope simply reports
            // has_more:false — the uniform contract still holds for agents.
            return ZendeskLean.BuildOffsetListEnvelope(json, "custom_statuses", null, parsedDetail,
                MaxResponseChars("custom_statuses_list"));
        });

    /// <summary>Returns a Zendesk custom ticket status by id.</summary>
    [McpServerTool(Name = "custom_statuses_get", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Single custom ticket status by id — full-detail record (null fields and API self-links omitted; absent " +
        "field means null/empty): agent and end-user labels (incl. raw_* dynamic-content variants), " +
        "descriptions, status category behind a ticket's custom_status_id. Read-only.")]
    public Task<JsonElement> Read(
        [Description("The numeric Zendesk custom status id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var request = zendesk.Api.V2.Custom_statuses[id].ToGetRequestInformation();
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("custom_status", out var status)
                ? ZendeskLean.EnsureWithinBudget(ZendeskLean.ToFullView(status), "custom_statuses_get",
                    MaxResponseChars("custom_statuses_get"), "Record exceeds the response budget.")
                : throw new McpException($"Zendesk custom status '{id}' was not found.");
        });

    /// <summary>Resolves the response-size budget for a tool (see <see cref="McpToolsOptions.GetMaxResponseChars" />).</summary>
    private int MaxResponseChars(string toolName) => mcpOptions.CurrentValue.Tools.GetMaxResponseChars(toolName);
}