using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for Zendesk ticket forms. Namespaced <c>forms_*</c> to mirror the Zendesk API structure.
/// </summary>
[McpServerToolType]
public sealed class ZendeskFormTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Lists Zendesk ticket forms.</summary>
    [McpServerTool(Name = "forms_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists Zendesk ticket forms (id, name, display name, active/default flags, and the ticket field ids on " +
        "each form). Cursor pagination: pass pageSize/afterCursor; the result's meta.has_more/meta.after_cursor " +
        "drive continuation. Read-only.")]
    public Task<ZendeskTicketFormsResult> Search(
        [Description("The cursor page size (max 100).")]
        int? pageSize = null,
        [Description("The cursor from the previous page's meta.after_cursor (optional).")]
        string? afterCursor = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Forms.ListAsync(pageSize, afterCursor, cancellationToken: cancellationToken));

    /// <summary>Returns a Zendesk ticket form by id.</summary>
    [McpServerTool(Name = "forms_get", ReadOnly = true, OpenWorld = true)]
    [Description("Returns a single Zendesk ticket form by numeric id. Read-only.")]
    public Task<ZendeskTicketForm> Read(
        [Description("The numeric Zendesk ticket form id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Forms.GetByIdAsync(id, cancellationToken));
}