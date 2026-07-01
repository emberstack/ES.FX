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
    public Task<ZendeskTicketFieldsResult> List(
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description("Results per page (default 100, max 100).")]
        int? perPage = 100,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.TicketFields.ListAsync(page, perPage, cancellationToken));

    /// <summary>Returns a single ticket field definition by id.</summary>
    [McpServerTool(Name = "zendesk_ticket_fields_read", ReadOnly = true, OpenWorld = true)]
    [Description("Returns a single ticket field definition by id (title, type, and options). Read-only.")]
    public Task<ZendeskTicketField> Read(
        [Description("The numeric ticket field id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.TicketFields.GetByIdAsync(id, cancellationToken));
}