using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP write tools for Zendesk macros (canned responses — admin configuration). Namespaced
///     <c>macros_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskMacroWriteTools(IZendeskClient zendeskApiClient, IMcpExecutionModeAccessor executionMode)
{
    /// <summary>Creates a Zendesk macro.</summary>
    [McpServerTool(Name = "macros_create", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Creates a Zendesk macro (canned response / bulk action). This changes account-wide configuration — the " +
        "macro becomes available to agents everywhere, it does not touch any ticket. 'title' and 'actions' are " +
        "required; each action is a { field, value } pair (e.g. { \"field\": \"status\", \"value\": \"solved\" }). " +
        "Returns the created macro. Write operation — honors the server execution mode: rejected in read-only " +
        "mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Create(
        [Description("The macro to create. 'title' and 'actions' are required.")]
        ZendeskMacroWrite macro,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create macro '{macro.Title}'",
            () => zendeskApiClient.Macros.CreateAsync(macro, cancellationToken: cancellationToken),
            macro);

    /// <summary>Updates a Zendesk macro.</summary>
    [McpServerTool(Name = "macros_update", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Updates a Zendesk macro by id. This changes account-wide configuration, not a single ticket. WARNING: " +
        "sending 'actions' replaces the macro's whole action array — read the current macro with " +
        "macros_get first and include ALL actions when changing any one. Returns the updated macro. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes " +
        "made) in dry-run mode.")]
    public Task<object> Update(
        [Description("The numeric macro id.")] long id,
        [Description(
            "The macro properties to update. If 'actions' is sent it replaces the entire action array.")]
        ZendeskMacroWrite macro,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"update macro {id}",
            () => zendeskApiClient.Macros.UpdateAsync(id, macro, cancellationToken: cancellationToken),
            new { id, macro });

    /// <summary>Deletes a Zendesk macro.</summary>
    [McpServerTool(Name = "macros_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Deletes a Zendesk macro by id. This changes account-wide configuration — the macro disappears for every " +
        "agent; tickets it was previously applied to are unaffected. Returns a completion acknowledgement. Write " +
        "operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in " +
        "dry-run mode.")]
    public Task<object> Delete(
        [Description("The numeric macro id.")] long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"delete macro {id}",
            () => zendeskApiClient.Macros.DeleteAsync(id, cancellationToken: cancellationToken),
            new { id });
}
