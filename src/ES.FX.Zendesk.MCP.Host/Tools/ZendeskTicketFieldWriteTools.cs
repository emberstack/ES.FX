using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tools.Models;
using ES.FX.Zendesk.Support;
using ES.FX.Zendesk.Support.Models;
using Microsoft.Kiota.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP write tools for Zendesk ticket field definitions (admin configuration). Namespaced
///     <c>ticket_fields_*</c>.
/// </summary>
/// <remarks>
///     The published spec models these POST/PUT operations without request bodies, so the generated builders only
///     provide the <see cref="RequestInformation" /> skeleton; the <c>{ "ticket_field": ... }</c> /
///     <c>{ "custom_field_option": ... }</c> envelopes documented at
///     https://developer.zendesk.com/api-reference/ticketing/tickets/ticket_fields/ are attached via the
///     generated wrapper models (spec-anomaly ledger row in <c>src/ES.FX.Zendesk/OpenApi/README.md</c>). Responses
///     come back as raw JSON (not a generated-model round-trip) because Kiota re-serialization drops
///     spec-read-only properties — most critically the created field/option <c>id</c>. Create/update responses
///     are then projected to lean confirmations (identity fields, plus for updates the server-state values of
///     exactly the fields the request set) — the complete definition stays one <c>ticket_fields_get</c> away.
/// </remarks>
[McpServerToolType]
public sealed class ZendeskTicketFieldWriteTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IMcpExecutionModeAccessor executionMode)
{
    /// <summary>
    ///     The echoed-option cap on write confirmations — enough to verify the option set landed (with its
    ///     assigned ids) without echoing a country-sized drop-down; the complete set stays reachable via
    ///     <c>ticket_fields_get</c>.
    /// </summary>
    private const int MaxEchoedOptions = 20;

    /// <summary>The <c>options_truncated</c> recovery pointer for write confirmations.</summary>
    private const string EchoedOptionsRecovery = "read the complete definition with ticket_fields_get";

    /// <summary>Creates a Zendesk ticket field.</summary>
    [McpServerTool(Name = "ticket_fields_create", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a ticket field definition (admin-only; account-wide config, not one ticket). Required: type, " +
        "title. type immutable after creation: text (default)|textarea|checkbox|date|integer|decimal|regexp|" +
        "partialcreditcard|multiselect|tagger (single-select dropdown)|lookup (relationship to another object). " +
        "tagger/multiselect: custom_field_options required at creation, each needs name (label) + value (tag). " +
        "checkbox: tag = tag added when checked. regexp: regexp_for_validation = pattern a value must match. " +
        "Perf: cap 400 ticket fields (per account without ticket forms, or per form when forms enabled). " +
        "Returns lean confirmation {id,type,title,created_at}; requested options echoed with assigned ids, " +
        "capped at 20 with options_truncated marker — full definition via ticket_fields_get. Honors execution " +
        "mode: rejected in read-only, simulated (no changes) in dry-run.")]
    public Task<object> Create(
        [Description("The ticket field to create. type and title required; type immutable afterwards.")]
        ZendeskTicketFieldWrite field,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create ticket field '{field.Title}' of type '{field.Type}'",
            async () =>
            {
                var request = zendesk.Api.V2.Ticket_fields.ToPostRequestInformation();
                request.SetContentFromParsable(requestAdapter, "application/json",
                    new TicketFieldResponse { TicketField = Map(field) });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildConfirmation(json, "create", field.CustomFieldOptions is not null,
                    "id", "type", "title", "created_at");
            },
            field);

    /// <summary>Updates a Zendesk ticket field.</summary>
    [McpServerTool(Name = "ticket_fields_update", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Updates a ticket field definition by id (admin-only; account-wide config, not one ticket). type " +
        "immutable. WARNING: sending custom_field_options REPLACES the whole option set — omitted options are " +
        "DELETED and their values removed from tickets; read current field via ticket_fields_get first and send " +
        "every option to keep, or use ticket_fields_options_create_or_update to change one option safely. Returns " +
        "lean confirmation {id,updated_at} plus server-state values of exactly the fields you sent (a value " +
        "differing from what you sent reveals a business-rule override); echoed options capped at 20 — full " +
        "definition via ticket_fields_get. Honors execution mode: rejected in read-only, simulated (no changes) " +
        "in dry-run.")]
    public Task<object> Update(
        [Description("The numeric ticket field id.")]
        long id,
        [Description(
            "Ticket field properties to update. Omit custom_field_options unless replacing the entire option set.")]
        ZendeskTicketFieldWrite field,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"update ticket field {id}",
            async () =>
            {
                var request = zendesk.Api.V2.Ticket_fields[id].ToPutRequestInformation();
                request.SetContentFromParsable(requestAdapter, "application/json",
                    new TicketFieldResponse { TicketField = Map(field) });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildConfirmation(json, "update", field.CustomFieldOptions is not null,
                    ["id", "updated_at", .. RequestedFields(field)]);
            },
            new { id, field });

    /// <summary>Deletes a Zendesk ticket field.</summary>
    [McpServerTool(Name = "ticket_fields_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Deletes a ticket field definition by id (admin-only; account-wide config). Field and its values " +
        "disappear from every ticket, not just one. IRREVERSIBLE — recreating the field does not restore lost " +
        "values. Returns acknowledgement carrying the deleted field's id. Honors execution mode: rejected in " +
        "read-only, simulated (no changes) in dry-run.")]
    public Task<object> Delete(
        [Description("The numeric ticket field id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"delete ticket field {id}",
            async () =>
            {
                await zendesk.Api.V2.Ticket_fields[id].DeleteAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return new ZendeskWriteAcknowledgement
                {
                    Description = $"Zendesk accepted the request to delete ticket field {id}.",
                    Id = id
                };
            },
            new { id });

    /// <summary>Creates or updates a single custom field option on a drop-down ticket field.</summary>
    [McpServerTool(Name = "ticket_fields_options_create_or_update", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Creates or updates a single custom field option on a drop-down (tagger/multiselect) ticket field " +
        "(admin-only; account-wide config, not one ticket). Upsert: include option id to update, omit to create. " +
        "Rate-limited 100 calls/min. Each option needs name (label) + value (tag). Max 2000 options per field. " +
        "Safer than replacing the whole option set via ticket_fields_update. Returns the created/updated custom " +
        "field option. Honors execution mode: rejected in read-only, simulated (no changes) in dry-run.")]
    public Task<object> SetOption(
        [Description("The numeric id of the drop-down ticket field that owns the option.")]
        long ticketFieldId,
        [Description("The option to create or update. Include id to update an existing option; omit to create.")]
        ZendeskCustomFieldOptionWrite option,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create or update option '{option.Value}' on ticket field {ticketFieldId}",
            async () =>
            {
                var request = zendesk.Api.V2.Ticket_fields[ticketFieldId].OptionsPath.ToPostRequestInformation();
                request.SetContentFromParsable(requestAdapter, "application/json",
                    new CustomFieldOptionResponse { CustomFieldOption = Map(option) });
                return Unwrap(await requestAdapter.SendForJsonAsync(request, cancellationToken)
                    .ConfigureAwait(false), "custom_field_option");
            },
            new { ticketFieldId, option });

    /// <summary>Deletes a custom field option from a drop-down ticket field.</summary>
    [McpServerTool(Name = "ticket_fields_options_delete", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Deletes a single custom field option from a drop-down (tagger/multiselect) ticket field by option id " +
        "(admin-only; account-wide config). The option's value is removed from every ticket that had it " +
        "selected. IRREVERSIBLE — recreating the option does not restore removed values. Returns acknowledgement " +
        "carrying the deleted option's id. Honors execution mode: rejected in read-only, simulated (no changes) " +
        "in dry-run.")]
    public Task<object> DeleteOption(
        [Description("The numeric id of the drop-down ticket field that owns the option.")]
        long ticketFieldId,
        [Description("The numeric id of the custom field option to delete.")]
        long optionId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"delete option {optionId} from ticket field {ticketFieldId}",
            async () =>
            {
                await zendesk.Api.V2.Ticket_fields[ticketFieldId].OptionsPath[optionId]
                    .DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                return new ZendeskWriteAcknowledgement
                {
                    Description =
                        $"Zendesk accepted the request to delete option {optionId} from ticket field {ticketFieldId}.",
                    Id = optionId
                };
            },
            new { ticketFieldId, optionId });

    /// <summary>
    ///     Maps the curated write model onto the generated request shape. Kiota omits unassigned (null)
    ///     properties on the wire — parity with the retired client's omit-null serializer.
    /// </summary>
    private static TicketFieldObject Map(ZendeskTicketFieldWrite field) => new()
    {
        Type = field.Type,
        Title = field.Title,
        Description = field.Description,
        Position = (int?)field.Position,
        Active = field.Active,
        Required = field.Required,
        VisibleInPortal = field.VisibleInPortal,
        EditableInPortal = field.EditableInPortal,
        RequiredInPortal = field.RequiredInPortal,
        Tag = field.Tag,
        RegexpForValidation = field.RegexpForValidation,
        // Ordering matters: Zendesk takes the array order as the option display order.
        CustomFieldOptions = field.CustomFieldOptions?.Select(Map).ToList()
    };

    private static CustomFieldOptionObject Map(ZendeskCustomFieldOptionWrite option)
    {
        var mapped = new CustomFieldOptionObject
        {
            Name = option.Name,
            Value = option.Value,
            Position = (int?)option.Position,
            AllowSolving = option.AllowSolving
        };
        // The generated model treats 'id' as read-only (never serialized), but the upsert contract requires it
        // in the body to update an existing option — route it through AdditionalData to reach the wire.
        if (option.Id is { } optionId) mapped.AdditionalData["id"] = optionId;
        return mapped;
    }

    /// <summary>
    ///     Unwraps a single-object envelope, mirroring the retired client's behavior of returning the bare
    ///     created/updated object and failing loudly on an empty payload.
    /// </summary>
    private static JsonElement Unwrap(JsonElement envelope, string propertyName)
    {
        if (envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty(propertyName, out var value) &&
            value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            return value;
        throw new McpException("The Zendesk API returned an empty response where a payload was expected.");
    }

    /// <summary>
    ///     The wire names of the fields present on the request (minus <c>custom_field_options</c>, which gets its
    ///     own capped echo) — the update confirmation's echo-of-change set (the server-state value of each
    ///     requested field is echoed back, revealing overrides).
    /// </summary>
    private static IEnumerable<string> RequestedFields(ZendeskTicketFieldWrite field)
    {
        if (field.Type is not null) yield return "type";
        if (field.Title is not null) yield return "title";
        if (field.Description is not null) yield return "description";
        if (field.Position is not null) yield return "position";
        if (field.Active is not null) yield return "active";
        if (field.Required is not null) yield return "required";
        if (field.VisibleInPortal is not null) yield return "visible_in_portal";
        if (field.EditableInPortal is not null) yield return "editable_in_portal";
        if (field.RequiredInPortal is not null) yield return "required_in_portal";
        if (field.Tag is not null) yield return "tag";
        if (field.RegexpForValidation is not null) yield return "regexp_for_validation";
    }

    /// <summary>
    ///     Unwraps the <c>{ "ticket_field": ... }</c> response envelope and projects the lean write confirmation:
    ///     the named fields, in order, present-and-non-null only. When <paramref name="echoOptions" /> is set
    ///     (the request carried <c>custom_field_options</c>) the server-state options — now carrying their
    ///     assigned ids — are echoed too, capped at <see cref="MaxEchoedOptions" /> with the
    ///     <c>options_truncated</c> marker. The final full-view pass strips option self-links and nulls.
    /// </summary>
    private static JsonElement BuildConfirmation(JsonElement response, string action, bool echoOptions,
        params string[] fields)
    {
        if (response.ValueKind is not JsonValueKind.Object ||
            !response.TryGetProperty("ticket_field", out var ticketField) ||
            ticketField.ValueKind is not JsonValueKind.Object)
            throw new McpException(
                $"Zendesk returned an unexpected response for the ticket field {action} — the change may still " +
                "have been applied; verify with ticket_fields_get.");

        var source = (JsonObject)JsonNode.Parse(ticketField.GetRawText())!;
        if (echoOptions)
        {
            ZendeskTicketFieldTools.CapFieldOptions(source, MaxEchoedOptions, EchoedOptionsRecovery);
            fields = [.. fields, "custom_field_options", "options_truncated"];
        }

        var confirmation = new JsonObject();
        foreach (var field in fields)
            if (source[field] is { } value)
                confirmation[field] = value.DeepClone();
        return ZendeskLean.ToFullView(JsonSerializer.SerializeToElement(confirmation));
    }
}