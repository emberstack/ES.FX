using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP write tools for Zendesk ticket forms (admin configuration). Namespaced <c>zendesk_forms_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskFormWriteTools(IZendeskClient zendeskApiClient, IMcpExecutionModeAccessor executionMode)
{
    /// <summary>Creates a Zendesk ticket form.</summary>
    [McpServerTool(Name = "zendesk_forms_create", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Creates a Zendesk ticket form (admin-only; requires a plan with multiple ticket forms). This changes " +
        "account-wide configuration, not a single ticket. 'name' is required; optionally set display_name, " +
        "position, active/default/end_user_visible/in_all_brands flags and the ordered ticket_field_ids " +
        "(resolve field ids with zendesk_ticket_fields_list). Returns the created ticket form. Write operation — " +
        "honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Create(
        [Description("The ticket form to create. 'name' is required.")]
        ZendeskTicketFormWrite form,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create ticket form '{form.Name}'",
            () => zendeskApiClient.Forms.CreateAsync(form, cancellationToken: cancellationToken),
            form);

    /// <summary>Updates a Zendesk ticket form.</summary>
    [McpServerTool(Name = "zendesk_forms_update", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Updates a Zendesk ticket form by id (admin-only). This changes account-wide configuration, not a single " +
        "ticket. Only the properties set in the payload are changed, but a supplied ticket_field_ids array " +
        "replaces the form's field list wholesale — read the current form with zendesk_forms_read first and send " +
        "the complete list. Returns the updated ticket form. Write operation — honors the server execution mode: " +
        "rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Update(
        [Description("The numeric Zendesk ticket form id.")]
        long id,
        [Description("The ticket form properties to update.")]
        ZendeskTicketFormWrite form,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"update ticket form {id}",
            () => zendeskApiClient.Forms.UpdateAsync(id, form, cancellationToken: cancellationToken),
            new { id, form });

    /// <summary>Deletes a Zendesk ticket form.</summary>
    [McpServerTool(Name = "zendesk_forms_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Deletes a Zendesk ticket form by id (admin-only; the account's default form cannot be deleted). This " +
        "changes account-wide configuration, not a single ticket. Returns a completion acknowledgement. Write " +
        "operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in " +
        "dry-run mode.")]
    public Task<object> Delete(
        [Description("The numeric Zendesk ticket form id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"delete ticket form {id}",
            () => zendeskApiClient.Forms.DeleteAsync(id, cancellationToken: cancellationToken),
            new { id });

    /// <summary>Clones a Zendesk ticket form.</summary>
    [McpServerTool(Name = "zendesk_forms_clone", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Clones an existing Zendesk ticket form by id, creating a copy (admin-only). This changes account-wide " +
        "configuration, not a single ticket; each call creates a new form. Returns the newly created copy — " +
        "adjust it afterwards with zendesk_forms_update. Write operation — honors the server execution mode: " +
        "rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Clone(
        [Description("The numeric id of the Zendesk ticket form to clone.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"clone ticket form {id}",
            () => zendeskApiClient.Forms.CloneAsync(id, cancellationToken: cancellationToken),
            new { id });
}
