using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for Zendesk ticket field definitions. Namespaced <c>zendesk_ticket_fields_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskTicketFieldTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Lists ticket field definitions.</summary>
    [McpServerTool(Name = "zendesk_ticket_fields_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists ticket field definitions — maps custom field ids to human titles, types, and dropdown option " +
        "value→label pairs. Needed to interpret the raw custom field values stored on a ticket. Read-only.")]
    public Task<ZendeskTicketFieldsResult> List(CancellationToken cancellationToken)
        // Unpaginated on purpose: without paging params Zendesk returns ALL fields (accounts cap at ~400).
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.TicketFields.ListAsync(cancellationToken: cancellationToken));

    /// <summary>Returns a single ticket field definition by id.</summary>
    [McpServerTool(Name = "zendesk_ticket_fields_read", ReadOnly = true, OpenWorld = true)]
    [Description("Returns a single ticket field definition by id (title, type, and options). Read-only.")]
    public Task<ZendeskTicketField> Read(
        [Description("The numeric ticket field id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.TicketFields.GetByIdAsync(id, cancellationToken));

    /// <summary>Lists the custom options of a drop-down ticket field.</summary>
    [McpServerTool(Name = "zendesk_ticket_fields_options", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists the custom options of a drop-down ticket field (value→label pairs in the custom_field_options " +
        "envelope) — use to see the allowed values before setting the field on a ticket or editing options with " +
        "zendesk_ticket_fields_options_set. Read-only.")]
    public Task<ZendeskCustomFieldOptionsResult> Options(
        [Description("The numeric ticket field id (must be a drop-down/tagger field).")]
        long ticketFieldId,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 100, max 100). The total is in 'count'; a non-null 'next_page' means more pages.")]
        int? perPage = 100,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.TicketFields.GetOptionsAsync(ticketFieldId, page, perPage, cancellationToken));
}