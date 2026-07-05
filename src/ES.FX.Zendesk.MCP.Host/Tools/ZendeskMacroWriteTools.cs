using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tools.Models;
using ES.FX.Zendesk.Support;
using ES.FX.Zendesk.Support.Api.V2.Macros;
using ES.FX.Zendesk.Support.Api.V2.Macros.Item;
using ES.FX.Zendesk.Support.Models;
using Microsoft.Kiota.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP write tools for Zendesk macros (canned responses — admin configuration). Namespaced
///     <c>macros_*</c>.
/// </summary>
/// <remarks>
///     Request bodies are mapped onto the generated request models; the macro echoed back by Zendesk is parsed
///     as raw wire JSON (the published spec types a macro action's <c>value</c> as a string, while the live API
///     returns array values for multi-value actions, which the typed model would silently drop) and then
///     collapsed to a lean write confirmation — identity plus the server-state values of the scalar fields the
///     request set. The actions array is never echoed back: the agent just sent it, and a third copy (after the
///     agent's own composition and the request body) would dwarf the confirmation — <c>macros_get</c> is the
///     verification sink.
/// </remarks>
[McpServerToolType]
public sealed class ZendeskMacroWriteTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IMcpExecutionModeAccessor executionMode)
{
    /// <summary>Creates a Zendesk macro.</summary>
    [McpServerTool(Name = "macros_create", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Create a macro (canned response / bulk action). Account-wide config: macro becomes available to all " +
        "agents; touches no ticket. Required: title, actions. Each action = {field,value} " +
        "(e.g. {\"field\":\"status\",\"value\":\"solved\"}). To set a custom field use custom_fields_<id>; get the " +
        "id from ticket_fields_list. Returns lean confirmation ({id,title,active,created_at} " +
        "+ server-state of other scalar fields sent); actions NOT echoed — verify via macros_get. " +
        "Write op honoring server execution mode: rejected in read-only, simulated (no change) in dry-run.")]
    public Task<object> Create(
        [Description("Macro to create; title + actions required.")]
        ZendeskMacroWrite macro,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create macro '{macro.Title}'",
            async () =>
            {
                var request = zendesk.Api.V2.Macros.ToPostRequestInformation(
                    new MacrosPostRequestBody { Macro = Map(macro) });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildConfirmation(json, macro, "created_at");
            },
            macro);

    /// <summary>Updates a Zendesk macro.</summary>
    [McpServerTool(Name = "macros_update", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Update a macro by id. Account-wide config, not a single ticket. WARNING: sending actions REPLACES the " +
        "whole action array — macros_get first and include ALL actions when changing any one. To set a custom " +
        "field use custom_fields_<id>; get the id from ticket_fields_list. Returns lean " +
        "confirmation ({id,title,active,updated_at} + server-state of scalar fields sent); actions NOT echoed — " +
        "verify via macros_get. Write op honoring server execution mode: rejected in read-only, simulated " +
        "(no change) in dry-run.")]
    public Task<object> Update(
        [Description("Numeric macro id.")] long id,
        [Description("Properties to update; actions (if sent) replaces the entire action array.")]
        ZendeskMacroWrite macro,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"update macro {id}",
            async () =>
            {
                var request = zendesk.Api.V2.Macros[id].ToPutRequestInformation(
                    new WithMacro_PutRequestBody { Macro = Map(macro) });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildConfirmation(json, macro, "updated_at");
            },
            new { id, macro });

    /// <summary>Deletes a Zendesk macro.</summary>
    [McpServerTool(Name = "macros_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Delete a macro by id. Account-wide config: macro disappears for every agent; tickets it was applied to " +
        "are unaffected. Returns acknowledgement carrying the deleted id. Write op honoring server execution " +
        "mode: rejected in read-only, simulated (no change) in dry-run.")]
    public Task<object> Delete(
        [Description("Numeric macro id.")] long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"delete macro {id}",
            async () =>
            {
                await zendesk.Api.V2.Macros[id].DeleteAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                // Built here (not by the invoker's bodyless overload) so the acknowledgement carries the
                // structured id the agent should not have to parse back out of the description prose.
                return new ZendeskWriteAcknowledgement
                {
                    Description = $"Zendesk accepted the request to delete macro {id}.",
                    Id = id
                };
            },
            new { id });

    /// <summary>
    ///     Maps the curated macro write model onto the generated request model. Kiota omits unassigned
    ///     properties on the wire, matching the retired client's omit-null serializer. <c>position</c> is not
    ///     modeled on <see cref="MacroInput" /> although the docs list it as a writable create/update body
    ///     property (https://developer.zendesk.com/api-reference/ticketing/business-rules/macros/), so it rides
    ///     in <see cref="MacroInput.AdditionalData" /> and serializes as a top-level field (spec-anomaly ledger
    ///     row in <c>src/ES.FX.Zendesk/OpenApi/README.md</c>).
    /// </summary>
    private static MacroInput Map(ZendeskMacroWrite macro)
    {
        var input = new MacroInput
        {
            Title = macro.Title,
            Description = macro.Description,
            Active = macro.Active,
            Actions = macro.Actions?.Select(Map).ToList()
        };
        if (macro.Position is { } position) input.AdditionalData["position"] = position;
        return input;
    }

    /// <summary>
    ///     Maps a curated <c>{ field, value }</c> macro action. The generated <see cref="ActionObject.Value" /> is
    ///     string-typed, but the live API also accepts array values for multi-value actions — e.g.
    ///     <c>comment_value</c> as <c>[channel, text]</c> per the Actions reference the spec itself links
    ///     (https://developer.zendesk.com/documentation/ticketing/reference-guides/actions-reference/) — so
    ///     non-string values ride in <see cref="ActionObject.AdditionalData" /> and serialize as the
    ///     <c>value</c> field verbatim (spec-anomaly ledger row in <c>src/ES.FX.Zendesk/OpenApi/README.md</c>).
    /// </summary>
    private static ActionObject Map(ZendeskMacroActionWrite action)
    {
        var mapped = new ActionObject { Field = action.Field };
        switch (action.Value)
        {
            case null:
                break;
            case string text:
                mapped.Value = text;
                break;
            case JsonElement { ValueKind: JsonValueKind.String } element:
                mapped.Value = element.GetString();
                break;
            default:
                mapped.AdditionalData["value"] = action.Value;
                break;
        }

        return mapped;
    }

    /// <summary>
    ///     Collapses the macro Zendesk echoes back to the lean write confirmation: identity
    ///     (<c>id</c>/<c>title</c>/<c>active</c> plus the relevant timestamp) and the server-state values of
    ///     exactly the scalar fields the request set (echo-of-change — reveals server-side normalization without
    ///     a follow-up get). The actions array is deliberately dropped (see the class remarks).
    /// </summary>
    private static JsonElement BuildConfirmation(JsonElement response, ZendeskMacroWrite write,
        string timestampField)
    {
        if (response.ValueKind is not JsonValueKind.Object || !response.TryGetProperty("macro", out var macro) ||
            macro.ValueKind is not JsonValueKind.Object)
            throw new McpException("The Zendesk API returned no macro where one was expected — the write may " +
                                   "still have been applied; verify with macros_get.");
        var source = (JsonObject)JsonNode.Parse(macro.GetRawText())!;

        var confirmation = new JsonObject();
        CopyServerState(source, confirmation, "id", "title", "active", timestampField);
        if (write.Description is not null) CopyServerState(source, confirmation, "description");
        if (write.Position is not null) CopyServerState(source, confirmation, "position");
        return JsonSerializer.SerializeToElement(confirmation);
    }

    /// <summary>Copies the named fields' server-state values when present and non-null, preserving the order.</summary>
    private static void CopyServerState(JsonObject source, JsonObject target, params string[] fields)
    {
        foreach (var field in fields)
            if (source[field] is { } value)
                target[field] = value.DeepClone();
    }
}