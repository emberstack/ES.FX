using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for Zendesk ticket forms. Namespaced <c>zendesk_forms_*</c> to mirror the Zendesk API structure.
/// </summary>
[McpServerToolType]
public sealed class ZendeskFormTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Lists all Zendesk ticket forms.</summary>
    [McpServerTool(Name = "zendesk_forms_search", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists all Zendesk ticket forms (id, name, display name, active/default flags, and the ticket field ids on " +
        "each form). Read-only.")]
    public Task<ZendeskTicketFormsResult> Search(CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Forms.ListAsync(cancellationToken: cancellationToken));

    /// <summary>Returns a Zendesk ticket form by id.</summary>
    [McpServerTool(Name = "zendesk_forms_read", ReadOnly = true, OpenWorld = true)]
    [Description("Returns a single Zendesk ticket form by numeric id. Read-only.")]
    public Task<ZendeskTicketForm> Read(
        [Description("The numeric Zendesk ticket form id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Forms.GetByIdAsync(id, cancellationToken));
}