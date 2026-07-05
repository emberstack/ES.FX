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
///     MCP write tools for Zendesk views (saved, shared ticket filters). Namespaced <c>views_*</c>.
/// </summary>
/// <remarks>
///     The published spec defines no request body for view create/update, so the generated builders expose no
///     request models. The tools attach the legacy <c>{ "view": { ... } }</c> JSON envelope (///     only fields the
///     caller set are sent) to the generated <see cref="RequestInformation" /> and parse then" /> and parse the
///     response as raw JSON — the generated <c>ViewObject</c> marks <c>id</c>/<c>created_at</c>/<c>updated_at</c>
///     read-only and would drop them on re-serialization. The echoed view is then collapsed to a lean write
///     confirmation — identity plus the server-state values of the scalar fields the request set. The
///     conditions/execution blocks are never echoed back: the agent just sent them, and a third copy (after the
///     agent's own composition and the request body) would dwarf the confirmation — <c>views_get</c> is the
///     verification sink.
/// </remarks>
[McpServerToolType]
public sealed class ZendeskViewWriteTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IMcpExecutionModeAccessor executionMode)
{
    /// <summary>
    ///     Serializer options for request bodies: <c>null</c> properties are omitted, so a partial update sends
    ///     only the fields the caller actually set (models carry explicit <c>JsonPropertyName</c> attributes).
    /// </summary>
    private static readonly JsonSerializerOptions WriteJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Creates a Zendesk view.</summary>
    [McpServerTool(Name = "views_create", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a Zendesk view (saved, shared ticket filter). Requires 'title' + >=1 'all' condition on " +
        "status|type|group_id|assignee_id|requester_id. Returns lean confirmation ({id,title,active,created_at} " +
        "+ server-state values of other scalar fields sent); conditions/execution layout NOT echoed — verify via " +
        "views_get. Inspect an existing view with views_get for the exact field/operator vocabulary. " +
        "Write op honoring server execution mode: rejected in read-only, simulated (no changes) in dry-run.")]
    public Task<object> Create(
        [Description(
            "View definition: title (required), description, active, 'all'/'any' condition arrays, output layout " +
            "(columns, group_by/sort_by).")]
        ZendeskViewWrite view,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"create view '{view.Title}'",
            async () =>
            {
                var request = WithViewEnvelope(zendesk.Api.V2.Views.ToPostRequestInformation(), view);
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildConfirmation(json, view, "created_at");
            }, view);

    /// <summary>Updates a Zendesk view by id.</summary>
    [McpServerTool(Name = "views_update", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Updates a Zendesk view by id. WARNING: 'all'/'any' condition arrays are replaced wholesale — read via " +
        "views_get first and send COMPLETE condition sets when touching any condition. Returns lean confirmation " +
        "({id,title,active,updated_at} + server-state values of scalar fields sent); conditions/execution layout " +
        "NOT echoed — verify via views_get. Write op honoring server execution mode: rejected in read-only, " +
        "simulated (no changes) in dry-run.")]
    public Task<object> Update(
        [Description("Numeric Zendesk view id.")]
        long id,
        [Description(
            "Fields to change. Condition arrays ('all'/'any') replaced wholesale — include every condition to keep.")]
        ZendeskViewWrite view,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"update view {id}",
            async () =>
            {
                var request = WithViewEnvelope(zendesk.Api.V2.Views[id].ToPutRequestInformation(), view);
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildConfirmation(json, view, "updated_at");
            }, new { id, view });

    /// <summary>Deletes a Zendesk view by id.</summary>
    [McpServerTool(Name = "views_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Deletes a Zendesk view by id. Admin config change, account-wide: a shared view disappears for every " +
        "agent using it. Returns acknowledgement carrying deleted id. Write op honoring server execution mode: " +
        "rejected in read-only, simulated (no changes) in dry-run.")]
    public Task<object> Delete(
        [Description("Numeric Zendesk view id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"delete view {id}",
            async () =>
            {
                await zendesk.Api.V2.Views[id].DeleteAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                // Built here (not by the invoker's bodyless overload) so the acknowledgement carries the
                // structured id the agent should not have to parse back out of the description prose.
                return new ZendeskWriteAcknowledgement
                {
                    Description = $"Zendesk accepted the request to delete view {id}.",
                    Id = id
                };
            }, new { id });

    /// <summary>
    ///     Attaches the <c>{ "view": { ... } }</c> write envelope as the JSON request body — the shape the live
    ///     API expects but the published spec (and therefore the generated builder) does not model.
    /// </summary>
    private static RequestInformation WithViewEnvelope(RequestInformation request, ZendeskViewWrite view)
    {
        request.SetStreamContent(
            new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new { view }, WriteJsonOptions)),
            "application/json");
        return request;
    }

    /// <summary>
    ///     Collapses the view Zendesk echoes back to the lean write confirmation: identity
    ///     (<c>id</c>/<c>title</c>/<c>active</c> plus the relevant timestamp) and the server-state values of
    ///     exactly the scalar fields the request set (echo-of-change — reveals server-side normalization without
    ///     a follow-up get). The conditions/execution blocks are deliberately dropped (see the class remarks).
    /// </summary>
    private static JsonElement BuildConfirmation(JsonElement response, ZendeskViewWrite write,
        string timestampField)
    {
        if (response.ValueKind is not JsonValueKind.Object || !response.TryGetProperty("view", out var view) ||
            view.ValueKind is not JsonValueKind.Object)
            throw new McpException("The Zendesk API returned no view where one was expected — the write " +
                                   "may still have been applied; verify with views_get.");
        var source = (JsonObject)JsonNode.Parse(view.GetRawText())!;

        var confirmation = new JsonObject();
        CopyServerState(source, confirmation, "id", "title", "active", timestampField);
        if (write.Description is not null) CopyServerState(source, confirmation, "description");
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