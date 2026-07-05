using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tools.Models;
using ES.FX.Zendesk.Support;
using Microsoft.Kiota.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP write tools for Zendesk ticket forms (admin configuration). Namespaced <c>forms_*</c>.
/// </summary>
/// <remarks>
///     Requests are built through the generated request builders, but responses are returned as the raw wire
///     JSON (<see cref="ZendeskKiotaRequests.SendForJsonAsync" />) rather than round-tripped through the
///     generated models: the published spec marks server-assigned fields (<c>id</c>, <c>url</c>,
///     <c>created_at</c>/<c>updated_at</c>, ...) as read-only, so Kiota's serializer would silently drop them.
///     Responses are then projected to lean confirmations (see <see cref="BuildConfirmation" />) — clone, in
///     particular, no longer echoes the duplicated field list and condition trees; the complete form stays one
///     <c>forms_get</c> away.
/// </remarks>
[McpServerToolType]
public sealed class ZendeskFormWriteTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IMcpExecutionModeAccessor executionMode)
{
    /// <summary>
    ///     Serializer options for the ticket-form write body: <c>null</c> properties are omitted, so a partial
    ///     update sends only the fields the caller actually set (the curated model carries explicit
    ///     <c>JsonPropertyName</c> attributes) — parity with the retired client's write serializer.
    /// </summary>
    private static readonly JsonSerializerOptions WriteJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Creates a Zendesk ticket form.</summary>
    [McpServerTool(Name = "forms_create", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a Zendesk ticket form. Admin-only; needs a plan with multiple ticket forms. Account-wide config, " +
        "not a single ticket. Required: name. Optional: display_name, position, flags " +
        "active|default|end_user_visible|in_all_brands, ordered ticket_field_ids (resolve ids via " +
        "ticket_fields_list). Returns {id,name,active,created_at}; full form via forms_get. Write op honoring " +
        "server execution mode: rejected read-only, simulated (no change) dry-run.")]
    public Task<object> Create(
        [Description("Form to create. name required.")]
        ZendeskTicketFormWrite form,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create ticket form '{form.Name}'",
            async () =>
            {
                var request = WithTicketFormBody(zendesk.Api.V2.Ticket_forms.ToPostRequestInformation(), form);
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildConfirmation(json, "create", "id", "name", "active", "created_at");
            },
            form);

    /// <summary>Updates a Zendesk ticket form.</summary>
    [McpServerTool(Name = "forms_update", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Updates a Zendesk ticket form by id. Admin-only; account-wide config, not a single ticket. Only payload " +
        "properties change, but a supplied ticket_field_ids array replaces the field list wholesale — read current " +
        "form via forms_get first and send the complete list. Returns {id,updated_at} plus server-state values of " +
        "exactly the fields you sent. Write op honoring server execution mode: rejected read-only, simulated " +
        "(no change) dry-run.")]
    public Task<object> Update(
        [Description("Numeric ticket form id.")]
        long id,
        [Description("Form properties to update.")]
        ZendeskTicketFormWrite form,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"update ticket form {id}",
            async () =>
            {
                var request = WithTicketFormBody(zendesk.Api.V2.Ticket_forms[id].ToPutRequestInformation(), form);
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildConfirmation(json, "update", ["id", "updated_at", .. RequestedFields(form)]);
            },
            new { id, form });

    /// <summary>Deletes a Zendesk ticket form.</summary>
    [McpServerTool(Name = "forms_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Deletes a Zendesk ticket form by id. Admin-only; account default form cannot be deleted. Account-wide " +
        "config, not a single ticket. Returns acknowledgement carrying deleted form's id. Write op honoring server " +
        "execution mode: rejected read-only, simulated (no change) dry-run.")]
    public Task<object> Delete(
        [Description("Numeric ticket form id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"delete ticket form {id}",
            async () =>
            {
                await zendesk.Api.V2.Ticket_forms[id].DeleteAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return new ZendeskWriteAcknowledgement
                {
                    Description = $"Zendesk accepted the request to delete ticket form {id}.",
                    Id = id
                };
            },
            new { id });

    /// <summary>Clones a Zendesk ticket form.</summary>
    [McpServerTool(Name = "forms_clone", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Clones a Zendesk ticket form by id, creating a copy. Admin-only; account-wide config, not a single " +
        "ticket; each call creates a new form. Returns copy's {id,name,active,created_at} — duplicated field list " +
        "and condition trees are NOT echoed (read via forms_get); adjust the copy afterwards with forms_update. " +
        "Write op honoring server execution mode: rejected read-only, simulated (no change) dry-run.")]
    public Task<object> Clone(
        [Description("Numeric id of the ticket form to clone.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"clone ticket form {id}",
            async () =>
            {
                var json = await requestAdapter.SendForJsonAsync(
                        zendesk.Api.V2.Ticket_forms[id].Clone.ToPostRequestInformation(), cancellationToken)
                    .ConfigureAwait(false);
                return BuildConfirmation(json, "clone", "id", "name", "active", "created_at");
            },
            new { id });

    /// <summary>
    ///     Escape hatch: the published spec omits the request body on ticket form create/update, so the generated
    ///     builders expose no body parameter. The curated model is serialized into the documented
    ///     <c>{ "ticket_form": { ... } }</c> envelope
    ///     (https://developer.zendesk.com/api-reference/ticketing/tickets/ticket_forms/#create-ticket-form;
    ///     spec-anomaly ledger row in <c>src/ES.FX.Zendesk/OpenApi/README.md</c>) — omit-null, snake_case via its
    ///     <c>JsonPropertyName</c> attributes; <c>ticket_field_ids</c> order is preserved — and attached to the
    ///     generated request so path templating and the adapter pipeline stay intact.
    /// </summary>
    private static RequestInformation WithTicketFormBody(RequestInformation request, ZendeskTicketFormWrite form)
    {
        request.SetStreamContent(
            new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new { ticket_form = form }, WriteJsonOptions)),
            "application/json");
        return request;
    }

    /// <summary>
    ///     The wire names of the fields present on the request — the update confirmation's echo-of-change set
    ///     (the server-state value of each requested field is echoed back, revealing overrides).
    /// </summary>
    private static IEnumerable<string> RequestedFields(ZendeskTicketFormWrite form)
    {
        if (form.Name is not null) yield return "name";
        if (form.DisplayName is not null) yield return "display_name";
        if (form.Position is not null) yield return "position";
        if (form.Active is not null) yield return "active";
        if (form.Default is not null) yield return "default";
        if (form.EndUserVisible is not null) yield return "end_user_visible";
        if (form.InAllBrands is not null) yield return "in_all_brands";
        if (form.TicketFieldIds is not null) yield return "ticket_field_ids";
    }

    /// <summary>
    ///     Unwraps the <c>{ "ticket_form": ... }</c> response envelope and projects the lean write confirmation:
    ///     the named fields, in order, present-and-non-null only. Create/clone confirm with the identity fields
    ///     ({id, name, active, created_at}); update confirms with {id, updated_at} plus the server-state values
    ///     of exactly the fields present in the request (see <see cref="RequestedFields" />). A clone's
    ///     duplicated field list and condition trees are never echoed — <c>forms_get</c> is the sink for them.
    /// </summary>
    private static JsonElement BuildConfirmation(JsonElement response, string action, params string[] fields)
    {
        if (response.ValueKind is not JsonValueKind.Object ||
            !response.TryGetProperty("ticket_form", out var ticketForm) ||
            ticketForm.ValueKind is not JsonValueKind.Object)
            throw new McpException(
                $"Zendesk returned an unexpected response for the ticket form {action} — the change may still " +
                "have been applied; verify with forms_get.");

        var source = (JsonObject)JsonNode.Parse(ticketForm.GetRawText())!;
        var confirmation = new JsonObject();
        foreach (var field in fields)
            if (source[field] is { } value)
                confirmation[field] = value.DeepClone();
        return JsonSerializer.SerializeToElement(confirmation);
    }
}