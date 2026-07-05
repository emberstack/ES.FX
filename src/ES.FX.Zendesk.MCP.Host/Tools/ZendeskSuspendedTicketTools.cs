using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.Support;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP read tools for Zendesk suspended tickets — inbound messages held out of the ticket stream.
///     Namespaced <c>suspended_tickets_*</c>.
/// </summary>
/// <remarks>
///     Requests are built through the generated request builders, but responses are read as the raw wire
///     JSON (<see cref="ZendeskKiotaRequests.SendForJsonAsync" />) rather than round-tripped through the
///     generated models: the published spec marks server-assigned fields (<c>id</c>, <c>url</c>, <c>cause</c>,
///     <c>created_at</c>/<c>updated_at</c>, ...) as read-only, so Kiota's serializer would silently drop them
///     from the tool result. List responses are then projected through <see cref="ZendeskLean" /> into the
///     uniform lean envelope — summary rows by default (the raw inbound email <c>content</c> is stripped), the
///     complete record via <c>detail:'full'</c> or <c>suspended_tickets_get</c>, which caps the content behind
///     its own <c>maxContentChars</c> parameter.
/// </remarks>
[McpServerToolType]
public sealed class ZendeskSuspendedTicketTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IOptionsMonitor<McpOptions> mcpOptions)
{
    /// <summary>The uniform <c>detail</c> parameter description shared by the list/search tools.</summary>
    private const string DetailDescription =
        "Row detail: 'summary' (default, lean triage rows) | 'full' (complete records minus null fields and " +
        "API links).";

    /// <summary>Lists Zendesk suspended tickets.</summary>
    [McpServerTool(Name = "suspended_tickets_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists suspended tickets — inbound emails Zendesk held out of the ticket stream (e.g. spam suspicion, " +
        "automated senders, etc.) NOT yet tickets; each has a 'cause' explaining the suspension. Ids are " +
        "suspended-ticket ids, not ticket ids. Summary rows: id, subject, cause, author name/email, brand_id, " +
        "ticket_id, created_at; raw inbound email 'content' stripped — read it via suspended_tickets_get or " +
        "detail:'full'. Cursor pagination; response has_more/after_cursor drive continuation. Read-only.")]
    public Task<JsonElement> List(
        [Description("Results per page (default 25); no documented maximum for this endpoint.")]
        int? pageSize = 25,
        [Description(
            "Continuation cursor for the NEXT page — OMIT for first page. Opaque token copied verbatim from " +
            "previous response's after_cursor; not a page number, never guess or pass empty (invalid cursor " +
            "→ 400).")]
        string? afterCursor = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // Generator (not spec) gap: the OAS models cursor pagination here via the DualPaginationPage
            // deepObject parameter (oneOf integer|object), but Kiota flattens it to a scalar 'page' query
            // parameter — page[size]/page[after] go on through the escape hatch.
            var request = zendesk.Api.V2.Suspended_tickets.ToGetRequestInformation()
                .WithCursorPagination(pageSize, afterCursor);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildCursorListEnvelope(json, "suspended_tickets", parsedDetail,
                MaxResponseChars("suspended_tickets_list"));
        });

    /// <summary>Returns a Zendesk suspended ticket by id.</summary>
    [McpServerTool(Name = "suspended_tickets_get", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Single suspended ticket by id — full-detail record (null fields and API self-links omitted; absent " +
        "field = null/empty): 'cause' (why suspended), author, subject, raw inbound email 'content'. content " +
        "capped at maxContentChars (default 4000); capped content ends with a marker naming the re-call " +
        "(maxContentChars:0) returning it in full. id is a suspended-ticket id, not a ticket id. Read-only.")]
    public Task<JsonElement> Read(
        [Description(
            "Suspended-ticket's own auto-generated id (not a real ticket id); from suspended_tickets_list.")]
        long id,
        CancellationToken cancellationToken = default,
        [Description(
            "Char cap on raw email 'content' (default 4000; 0 = no limit). Capped content ends with a marker " +
            "naming the exact re-call (maxContentChars:0) returning it in full.")]
        int maxContentChars = 4000)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            if (maxContentChars < 0)
                throw new McpException(
                    $"Invalid maxContentChars value '{maxContentChars}'. Pass a positive character cap, or 0 " +
                    "for no limit.");

            var request = zendesk.Api.V2.Suspended_tickets[id].ToGetRequestInformation();
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return json.ValueKind == JsonValueKind.Object &&
                   json.TryGetProperty("suspended_ticket", out var suspendedTicket)
                ? ZendeskLean.EnsureWithinBudget(
                    CapContent(ZendeskLean.ToFullView(suspendedTicket), maxContentChars), "suspended_tickets_get",
                    MaxResponseChars("suspended_tickets_get"), "Record exceeds the response budget.")
                : throw new McpException($"Zendesk suspended ticket '{id}' was not found.");
        });

    /// <summary>Resolves the response-size budget for a tool (see <see cref="McpToolsOptions.GetMaxResponseChars" />).</summary>
    private int MaxResponseChars(string toolName) => mcpOptions.CurrentValue.Tools.GetMaxResponseChars(toolName);

    /// <summary>
    ///     Applies the <c>maxContentChars</c> cap (0 = no limit) to the raw inbound email (<c>content</c>) of a
    ///     full-view suspended ticket. A capped value ends with a self-describing marker naming the exact
    ///     re-call that returns the content in full.
    /// </summary>
    private static JsonElement CapContent(JsonElement suspendedTicket, int maxContentChars)
    {
        if (maxContentChars == 0) return suspendedTicket;
        if (JsonNode.Parse(suspendedTicket.GetRawText()) is not JsonObject ticket ||
            ticket["content"] is not JsonValue value || !value.TryGetValue(out string? content) ||
            content.Length <= maxContentChars)
            return suspendedTicket;

        ticket["content"] = ZendeskLean.TruncateWithMarker(content, maxContentChars,
            "re-call with maxContentChars:0 (0 = no limit) for the full content");
        return JsonSerializer.SerializeToElement(ticket);
    }
}