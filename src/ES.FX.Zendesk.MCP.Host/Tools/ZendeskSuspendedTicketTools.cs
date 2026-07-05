using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP read tools for Zendesk suspended tickets — inbound messages held out of the ticket stream.
///     Namespaced <c>suspended_tickets_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskSuspendedTicketTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Lists Zendesk suspended tickets.</summary>
    [McpServerTool(Name = "suspended_tickets_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists suspended tickets — inbound emails Zendesk held out of the ticket stream (spam suspicion, " +
        "automated senders, etc.) that are NOT tickets yet; each carries a 'cause' explaining the suspension. " +
        "Ids are suspended-ticket ids, not ticket ids. Cursor pagination: pass pageSize/afterCursor; the result's " +
        "meta.has_more/meta.after_cursor drive continuation. Read-only.")]
    public Task<ZendeskSuspendedTicketsResult> List(
        [Description("The cursor page size (optional, max 100).")]
        int? pageSize = null,
        [Description("The cursor from the previous page's meta.after_cursor (omit for the first page).")]
        string? afterCursor = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.SuspendedTickets.ListAsync(pageSize: pageSize, afterCursor: afterCursor,
                cancellationToken: cancellationToken));

    /// <summary>Returns a Zendesk suspended ticket by id.</summary>
    [McpServerTool(Name = "suspended_tickets_get", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns a single suspended ticket by id — an inbound email Zendesk held out of the ticket stream (it is " +
        "not a ticket yet), including its 'cause' (why it was suspended), author, subject, and content. The id is " +
        "a suspended-ticket id, not a ticket id. Read-only.")]
    public Task<ZendeskSuspendedTicket> Read(
        [Description("The numeric Zendesk suspended-ticket id (not a ticket id).")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.SuspendedTickets.GetByIdAsync(id, cancellationToken));
}
