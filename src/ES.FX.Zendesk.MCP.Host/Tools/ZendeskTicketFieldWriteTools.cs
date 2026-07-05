using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP write tools for Zendesk ticket field definitions (admin configuration). Namespaced
///     <c>ticket_fields_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskTicketFieldWriteTools(
    IZendeskClient zendeskApiClient,
    IMcpExecutionModeAccessor executionMode)
{
    /// <summary>Creates a Zendesk ticket field.</summary>
    [McpServerTool(Name = "ticket_fields_create", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Creates a Zendesk ticket field definition (admin-only). This changes account-wide configuration, not a " +
        "single ticket. 'type' and 'title' are required; 'type' (text, textarea, checkbox, date, integer, decimal, " +
        "regexp, multiselect, tagger, lookup...) is immutable after creation. For 'tagger'/'multiselect' fields, " +
        "custom_field_options are required at creation. Returns the created ticket field. Write operation — honors " +
        "the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Create(
        [Description("The ticket field to create. 'type' and 'title' are required; 'type' is immutable afterwards.")]
        ZendeskTicketFieldWrite field,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create ticket field '{field.Title}' of type '{field.Type}'",
            () => zendeskApiClient.TicketFields.CreateAsync(field, cancellationToken: cancellationToken),
            field);

    /// <summary>Updates a Zendesk ticket field.</summary>
    [McpServerTool(Name = "ticket_fields_update", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Updates a Zendesk ticket field definition by id (admin-only). This changes account-wide configuration, " +
        "not a single ticket. 'type' is immutable and cannot be changed. WARNING: sending custom_field_options " +
        "replaces the field's whole option set — omitted options are DELETED and their values removed from " +
        "tickets; read the current field with ticket_fields_get first and send every option you want to " +
        "keep, or use ticket_fields_options_create_or_update to change a single option safely. Returns the updated " +
        "ticket field. Write operation — honors the server execution mode: rejected in read-only mode, simulated " +
        "(no changes made) in dry-run mode.")]
    public Task<object> Update(
        [Description("The numeric ticket field id.")]
        long id,
        [Description(
            "The ticket field properties to update. Omit custom_field_options unless you intend to replace the " +
            "entire option set.")]
        ZendeskTicketFieldWrite field,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"update ticket field {id}",
            () => zendeskApiClient.TicketFields.UpdateAsync(id, field, cancellationToken: cancellationToken),
            new { id, field });

    /// <summary>Deletes a Zendesk ticket field.</summary>
    [McpServerTool(Name = "ticket_fields_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Deletes a Zendesk ticket field definition by id (admin-only). This changes account-wide configuration — " +
        "the field and its values disappear from every ticket, not just one. IRREVERSIBLE — recreating the field " +
        "does not restore the lost values. Returns a completion acknowledgement. Write operation — honors the " +
        "server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Delete(
        [Description("The numeric ticket field id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"delete ticket field {id}",
            () => zendeskApiClient.TicketFields.DeleteAsync(id, cancellationToken: cancellationToken),
            new { id });

    /// <summary>Creates or updates a single custom field option on a drop-down ticket field.</summary>
    [McpServerTool(Name = "ticket_fields_options_create_or_update", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = true)]
    [Description(
        "Creates or updates a single custom field option on a drop-down (tagger/multiselect) ticket field " +
        "(admin-only, upsert semantics: include the option 'id' to update an existing option, omit it to create " +
        "one; rate-limited to 100 calls/min). This changes account-wide configuration, not a single ticket. " +
        "Safer than replacing the whole option set via ticket_fields_update. Returns the created or " +
        "updated custom field option. Write operation — honors the server execution mode: rejected in read-only " +
        "mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> SetOption(
        [Description("The numeric id of the drop-down ticket field that owns the option.")]
        long ticketFieldId,
        [Description("The option to create or update. Include 'id' to update an existing option; omit to create.")]
        ZendeskCustomFieldOptionWrite option,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create or update option '{option.Value}' on ticket field {ticketFieldId}",
            () => zendeskApiClient.TicketFields.CreateOrUpdateOptionAsync(ticketFieldId, option,
                cancellationToken: cancellationToken),
            new { ticketFieldId, option });

    /// <summary>Deletes a custom field option from a drop-down ticket field.</summary>
    [McpServerTool(Name = "ticket_fields_options_delete", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = true)]
    [Description(
        "Deletes a single custom field option from a drop-down (tagger/multiselect) ticket field by option id " +
        "(admin-only). This changes account-wide configuration — the option's value is removed from every ticket " +
        "that had it selected. IRREVERSIBLE — recreating the option does not restore the removed values. Returns " +
        "a completion acknowledgement. Write operation — honors the server execution mode: rejected in read-only " +
        "mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> DeleteOption(
        [Description("The numeric id of the drop-down ticket field that owns the option.")]
        long ticketFieldId,
        [Description("The numeric id of the custom field option to delete.")]
        long optionId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"delete option {optionId} from ticket field {ticketFieldId}",
            () => zendeskApiClient.TicketFields.DeleteOptionAsync(ticketFieldId, optionId,
                cancellationToken: cancellationToken),
            new { ticketFieldId, optionId });
}
