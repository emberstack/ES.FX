using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.MCP.Host.Execution;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP write tools for Zendesk suspended tickets (inbound messages held out of the ticket stream).
///     Namespaced <c>suspended_tickets_*</c>. Ids are suspended-ticket ids, NOT ticket ids.
/// </summary>
[McpServerToolType]
public sealed class ZendeskSuspendedTicketWriteTools(
    IZendeskClient zendeskApiClient,
    IMcpExecutionModeAccessor executionMode)
{
    /// <summary>Recovers a suspended ticket into a real ticket.</summary>
    [McpServerTool(Name = "suspended_tickets_recover", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Recovers a suspended ticket into a real ticket by suspended-ticket id (NOT a ticket id). Synchronous (not " +
        "an async job): 200 with a tickets array on success, 422 with a suspended_tickets array on failure. SIDE " +
        "EFFECT: recovery sets the recovered ticket's requester to the calling agent (not the original requester) to " +
        "prevent re-suspension — use suspended_tickets_recover_many with a single id to preserve the original " +
        "requester. Returns the recovery result with the recovered ticket. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Recover(
        [Description(
            "The suspended-ticket's own auto-generated id (from suspended_tickets_list; not a real ticket id).")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"recover suspended ticket {id}",
            () => zendeskApiClient.SuspendedTickets.RecoverAsync(id, cancellationToken: cancellationToken),
            new { id });

    /// <summary>Recovers up to 100 suspended tickets, preserving their original requesters.</summary>
    [McpServerTool(Name = "suspended_tickets_recover_many", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Recovers up to 100 suspended tickets by suspended-ticket ids (synchronous — not an async job). Unlike " +
        "suspended_tickets_recover, this PRESERVES the original requesters, so prefer it even for a single " +
        "id when the requester matters. Returns the recovery result with the recovered tickets. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> RecoverMany(
        [Description(
            "The suspended-ticket auto-generated ids to recover (min 1, max 100; not ticket ids). Suspended tickets " +
            "that fail to recover are still included in the response.")]
        long[] ids,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"recover {ids.Length} suspended tickets",
            () => zendeskApiClient.SuspendedTickets.RecoverManyAsync(ids, cancellationToken: cancellationToken),
            new { ids });

    /// <summary>Deletes a suspended ticket by id.</summary>
    [McpServerTool(Name = "suspended_tickets_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Deletes a suspended ticket by suspended-ticket id (NOT a ticket id), permanently discarding the held " +
        "message. Returns a completion acknowledgement. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Delete(
        [Description(
            "The suspended-ticket's own auto-generated id (from suspended_tickets_list; not a real ticket id).")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"delete suspended ticket {id}",
            () => zendeskApiClient.SuspendedTickets.DeleteAsync(id, cancellationToken: cancellationToken),
            new { id });

    /// <summary>Deletes up to 100 suspended tickets.</summary>
    [McpServerTool(Name = "suspended_tickets_delete_many", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Deletes up to 100 suspended tickets by suspended-ticket ids (synchronous — completes immediately, not an " +
        "async job), permanently discarding the held messages. Returns a completion acknowledgement. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> DeleteMany(
        [Description(
            "The suspended-ticket auto-generated ids to delete (min 1, max 100; not ticket ids).")]
        long[] ids,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"delete {ids.Length} suspended tickets",
            () => zendeskApiClient.SuspendedTickets.DeleteManyAsync(ids, cancellationToken: cancellationToken),
            new { ids });
}
