using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP write tools for Zendesk custom ticket statuses. Namespaced <c>zendesk_custom_statuses_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskCustomStatusWriteTools(
    IZendeskClient zendeskApiClient,
    IMcpExecutionModeAccessor executionMode)
{
    /// <summary>Creates a Zendesk custom ticket status.</summary>
    [McpServerTool(Name = "zendesk_custom_statuses_create", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Creates a Zendesk custom ticket status (admin-only). 'status_category' (new/open/pending/hold/solved) and " +
        "'agent_label' (max 48 chars) are required; status_category CANNOT be changed after creation. Returns the " +
        "created custom status. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Create(
        [Description(
            "The custom status definition: status_category and agent_label (required), plus optional " +
            "end_user_label, description, end_user_description, and active.")]
        ZendeskCustomStatusWrite status,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"create custom ticket status '{status.AgentLabel}'",
            () => zendeskApiClient.CustomStatuses.CreateAsync(status, cancellationToken: cancellationToken), status);

    /// <summary>Updates a Zendesk custom ticket status by id.</summary>
    [McpServerTool(Name = "zendesk_custom_statuses_update", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Updates a Zendesk custom ticket status by id (admin-only). 'status_category' cannot be changed; deactivate " +
        "a status by setting active = false. Returns the updated custom status. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Update(
        [Description("The numeric Zendesk custom status id.")]
        long id,
        [Description(
            "The fields to change (agent_label, end_user_label, description, end_user_description, active). " +
            "status_category is immutable.")]
        ZendeskCustomStatusWrite status,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"update custom ticket status {id}",
            () => zendeskApiClient.CustomStatuses.UpdateAsync(id, status, cancellationToken: cancellationToken),
            new { id, status });

    /// <summary>Deletes a Zendesk custom ticket status by id.</summary>
    [McpServerTool(Name = "zendesk_custom_statuses_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Deletes a Zendesk custom ticket status by id (admin-only). Zendesk rejects the delete unless the status " +
        "has first been unassigned from all non-closed tickets. Returns a completion acknowledgement. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Delete(
        [Description("The numeric Zendesk custom status id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"delete custom ticket status {id}",
            () => zendeskApiClient.CustomStatuses.DeleteAsync(id, cancellationToken: cancellationToken), new { id });
}
