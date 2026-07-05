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
///     MCP tools for Zendesk ticket forms. Namespaced <c>forms_*</c> to mirror the Zendesk API structure.
/// </summary>
/// <remarks>
///     Requests are built through the generated request builders, but responses are returned as the raw wire
///     JSON (<see cref="ZendeskKiotaRequests.SendForJsonAsync" />) rather than round-tripped through the
///     generated models: the published spec marks server-assigned fields (<c>id</c>, <c>url</c>,
///     <c>created_at</c>/<c>updated_at</c>, ...) as read-only, so Kiota's serializer would silently drop them
///     from the tool result. Responses are then projected through <see cref="ZendeskLean" /> into the uniform
///     lean envelope — summary rows (condition trees stripped) by default, complete forms via
///     <c>detail:'full'</c> or <c>forms_get</c>.
/// </remarks>
[McpServerToolType]
public sealed class ZendeskFormTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IOptionsMonitor<McpOptions> mcpOptions)
{
    /// <summary>The uniform <c>detail</c> parameter description shared by the list tools.</summary>
    private const string DetailDescription =
        "Row detail: 'summary' (default, lean triage rows) | 'full' (complete records, null fields+API links " +
        "omitted).";

    /// <summary>Lists Zendesk ticket forms.</summary>
    [McpServerTool(Name = "forms_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Ticket forms as lean summary rows (id, name, active/default flags, position, ordered ticket_field_ids); " +
        "conditional-logic trees and end-user text stripped. detail:'full' for complete records, or forms_get " +
        "for one. Resolve field ids via ticket_fields_get_many. Cursor pagination: response has_more/after_cursor " +
        "drive continuation.")]
    public Task<JsonElement> Search(
        [Description("Results per page (default 25; max 100).")]
        int? pageSize = 25,
        [Description(
            "Continuation cursor for the NEXT page; OMIT for first page. Opaque token copied verbatim from prior " +
            "response's after_cursor; not a page number, don't guess or pass empty (invalid cursor→400).")]
        string? afterCursor = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // Escape hatch for a generator (not spec) gap: the OAS models cursor paging on ticket_forms via the
            // DualPaginationPage parameter (deepObject page[size]/page[after]/page[before]), but Kiota collapses
            // the discriminator-less oneOf [integer, object] query parameter to a plain scalar 'page', so the
            // generated builder cannot emit page[size]/page[after].
            var request = zendesk.Api.V2.Ticket_forms.ToGetRequestInformation()
                .WithCursorPagination(pageSize, afterCursor);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildCursorListEnvelope(json, "ticket_forms", parsedDetail,
                MaxResponseChars("forms_list"));
        });

    /// <summary>Returns a Zendesk ticket form by id.</summary>
    [McpServerTool(Name = "forms_get", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Single ticket form by numeric id; full-detail record (null fields and API self-links omitted): name, " +
        "flags, ordered ticket_field_ids, and the conditional-logic trees (end_user_conditions/agent_conditions) " +
        "that forms_list strips. Resolve field ids via ticket_fields_get_many.")]
    public Task<JsonElement> Read(
        [Description("Numeric ticket form id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var json = await requestAdapter.SendForJsonAsync(
                    zendesk.Api.V2.Ticket_forms[id].ToGetRequestInformation(), cancellationToken)
                .ConfigureAwait(false);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("ticket_form", out var form)
                ? ZendeskLean.ToFullView(form)
                : throw new McpException($"Zendesk ticket form '{id}' was not found.");
        });

    /// <summary>Resolves the response-size budget for a tool (see <see cref="McpToolsOptions.GetMaxResponseChars" />).</summary>
    private int MaxResponseChars(string toolName) => mcpOptions.CurrentValue.Tools.GetMaxResponseChars(toolName);
}