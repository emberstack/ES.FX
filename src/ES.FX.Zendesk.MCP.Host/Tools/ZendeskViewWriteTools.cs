using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP write tools for Zendesk views (saved, shared ticket filters). Namespaced <c>zendesk_views_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskViewWriteTools(IZendeskClient zendeskApiClient, IMcpExecutionModeAccessor executionMode)
{
    /// <summary>Creates a Zendesk view.</summary>
    [McpServerTool(Name = "zendesk_views_create", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Creates a Zendesk view (a saved, shared ticket filter). 'title' and at least one 'all' condition on " +
        "status/type/group_id/assignee_id/requester_id are required. Returns the created view. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Create(
        [Description(
            "The view definition: title (required), description, active, 'all'/'any' condition arrays, and output " +
            "layout (columns, group_by/sort_by).")]
        ZendeskViewWrite view,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"create view '{view.Title}'",
            () => zendeskApiClient.Views.CreateAsync(view, cancellationToken: cancellationToken), view);

    /// <summary>Updates a Zendesk view by id.</summary>
    [McpServerTool(Name = "zendesk_views_update", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Updates a Zendesk view by id. WARNING: the 'all'/'any' condition arrays are replaced wholesale — read the " +
        "view first with zendesk_views_read and send the COMPLETE condition sets when touching any condition. " +
        "Returns the updated view. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Update(
        [Description("The numeric Zendesk view id.")]
        long id,
        [Description(
            "The fields to change. Condition arrays ('all'/'any') are replaced wholesale, so include every " +
            "condition you want to keep.")]
        ZendeskViewWrite view,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"update view {id}",
            () => zendeskApiClient.Views.UpdateAsync(id, view, cancellationToken: cancellationToken),
            new { id, view });

    /// <summary>Deletes a Zendesk view by id.</summary>
    [McpServerTool(Name = "zendesk_views_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Deletes a Zendesk view by id. Admin configuration change with account-wide effect: a shared view " +
        "disappears for every agent that uses it. Returns a completion acknowledgement. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Delete(
        [Description("The numeric Zendesk view id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"delete view {id}",
            () => zendeskApiClient.Views.DeleteAsync(id, cancellationToken: cancellationToken), new { id });
}
