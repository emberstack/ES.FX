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
///     MCP write tools for Zendesk custom ticket statuses. Namespaced <c>custom_statuses_*</c>.
/// </summary>
/// <remarks>
///     Requests are built through the generated request builders and sent as raw wire JSON
///     (<see cref="ZendeskKiotaRequests.SendForJsonAsync" />) rather than round-tripped through the generated
///     models: the published spec marks server-assigned fields (custom status <c>id</c>,
///     <c>created_at</c>/<c>updated_at</c>, the raw label variants, ...) as read-only, so Kiota's serializer
///     would silently drop them. Write responses collapse to lean confirmations: create returns
///     <c>{id, status_category, agent_label, created_at}</c>; update returns <c>{id, updated_at}</c> plus the
///     server-state values of exactly the fields the request carried (echo-of-change — a value differing from
///     the request reveals a business-rule override without a follow-up <c>custom_statuses_get</c>).
/// </remarks>
[McpServerToolType]
public sealed class ZendeskCustomStatusWriteTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IMcpExecutionModeAccessor executionMode)
{
    /// <summary>Creates a Zendesk custom ticket status.</summary>
    [McpServerTool(Name = "custom_statuses_create", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Create Zendesk custom ticket status (admin-only). Required: status_category (new|open|pending|hold|solved), " +
        "agent_label (max 48 chars); status_category immutable after creation. Returns {id, status_category, " +
        "agent_label, created_at}; full record via custom_statuses_get. " +
        "Write op — rejected in read-only mode, simulated (no change) in dry-run mode.")]
    public Task<object> Create(
        [Description(
            "Definition: status_category, agent_label (required); optional end_user_label, description, " +
            "end_user_description, active.")]
        ZendeskCustomStatusWrite status,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"create custom ticket status '{status.AgentLabel}'",
            async () =>
            {
                var request = zendesk.Api.V2.Custom_statuses.ToPostRequestInformation(
                    new CustomStatusCreateRequest { CustomStatus = MapCreate(status) });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildConfirmation(json, "create", "id", "status_category", "agent_label", "created_at");
            },
            status);

    /// <summary>Updates a Zendesk custom ticket status by id.</summary>
    [McpServerTool(Name = "custom_statuses_update", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Update Zendesk custom ticket status by id (admin-only). status_category immutable; deactivate via " +
        "active=false. Returns {id, updated_at} plus server-state values of exactly the fields sent — a value " +
        "differing from your request reveals a business-rule override without a follow-up custom_statuses_get. " +
        "Write op — rejected in read-only mode, simulated (no change) in dry-run mode.")]
    public Task<object> Update(
        [Description("Numeric Zendesk custom status id.")]
        long id,
        [Description(
            "Fields to change: agent_label, end_user_label, description, end_user_description, active. " +
            "status_category immutable.")]
        ZendeskCustomStatusWrite status,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"update custom ticket status {id}",
            async () =>
            {
                var request = zendesk.Api.V2.Custom_statuses[id].ToPutRequestInformation(
                    new CustomStatusUpdateRequest { CustomStatus = MapUpdate(status) });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildConfirmation(json, "update", ["id", "updated_at", .. RequestedFields(status)]);
            },
            new { id, status });

    /// <summary>Deletes a Zendesk custom ticket status by id.</summary>
    [McpServerTool(Name = "custom_statuses_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Delete Zendesk custom ticket status by id (admin-only). Rejected unless status first unassigned from all " +
        "non-closed tickets. Returns acknowledgement carrying the structured status id. " +
        "Write op — rejected in read-only mode, simulated (no change) in dry-run mode.")]
    public Task<object> Delete(
        [Description("Numeric Zendesk custom status id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"delete custom ticket status {id}",
            // The shared bodyless overload cannot know the record id, so the acknowledgement is built here —
            // the structured id lets the agent chain the result without parsing it out of prose.
            async () =>
            {
                await zendesk.Api.V2.Custom_statuses[id].DeleteAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return new ZendeskWriteAcknowledgement
                {
                    Description = $"Zendesk accepted the request to delete custom ticket status {id}.",
                    Id = id
                };
            }, new { id });

    /// <summary>
    ///     Maps the curated write model onto the generated create payload. Unrecognized status_category values are
    ///     passed through verbatim via <c>AdditionalData</c> so Zendesk itself validates them, exactly like the
    ///     retired client's string pass-through did.
    /// </summary>
    private static CustomStatusCreateInput MapCreate(ZendeskCustomStatusWrite status)
    {
        var input = new CustomStatusCreateInput
        {
            StatusCategory = MapStatusCategory(status.StatusCategory),
            AgentLabel = status.AgentLabel,
            EndUserLabel = status.EndUserLabel,
            Description = status.Description,
            EndUserDescription = status.EndUserDescription,
            Active = status.Active
        };
        if (status.StatusCategory is not null && input.StatusCategory is null)
            input.AdditionalData["status_category"] = status.StatusCategory;
        return input;
    }

    /// <summary>
    ///     Maps the curated write model onto the generated update payload. The generated update input has no
    ///     status_category (it is immutable), but the retired client serialized the field when callers set it —
    ///     preserved through <c>AdditionalData</c> so Zendesk keeps deciding what to do with it.
    /// </summary>
    private static CustomStatusUpdateInput MapUpdate(ZendeskCustomStatusWrite status)
    {
        var input = new CustomStatusUpdateInput
        {
            AgentLabel = status.AgentLabel,
            EndUserLabel = status.EndUserLabel,
            Description = status.Description,
            EndUserDescription = status.EndUserDescription,
            Active = status.Active
        };
        if (status.StatusCategory is not null)
            input.AdditionalData["status_category"] = status.StatusCategory;
        return input;
    }

    private static CustomStatusCreateInput_status_category? MapStatusCategory(string? statusCategory) =>
        statusCategory switch
        {
            "new" => CustomStatusCreateInput_status_category.New,
            "open" => CustomStatusCreateInput_status_category.Open,
            "pending" => CustomStatusCreateInput_status_category.Pending,
            "hold" => CustomStatusCreateInput_status_category.Hold,
            "solved" => CustomStatusCreateInput_status_category.Solved,
            _ => null
        };

    /// <summary>
    ///     The wire names of the fields present on the request — the update confirmation's echo-of-change set
    ///     (the server-state value of each requested field is echoed back, revealing overrides; an echoed
    ///     status_category shows the immutable value Zendesk kept).
    /// </summary>
    private static IEnumerable<string> RequestedFields(ZendeskCustomStatusWrite status)
    {
        if (status.StatusCategory is not null) yield return "status_category";
        if (status.AgentLabel is not null) yield return "agent_label";
        if (status.EndUserLabel is not null) yield return "end_user_label";
        if (status.Description is not null) yield return "description";
        if (status.EndUserDescription is not null) yield return "end_user_description";
        if (status.Active is not null) yield return "active";
    }

    /// <summary>
    ///     Collapses a write response to the lean confirmation the tool returns: the named fields of the
    ///     response's <c>custom_status</c> member, in order, with null/absent fields omitted (absent = null/empty).
    /// </summary>
    private static JsonElement BuildConfirmation(JsonElement response, string action, params string[] fields)
    {
        if (response.ValueKind is not JsonValueKind.Object ||
            !response.TryGetProperty("custom_status", out var status) ||
            status.ValueKind is not JsonValueKind.Object)
            throw new McpException(
                $"Zendesk returned an unexpected response for the custom ticket status {action} — the change " +
                "may still have been applied; verify with custom_statuses_get.");

        var source = (JsonObject)JsonNode.Parse(status.GetRawText())!;
        var confirmation = new JsonObject();
        foreach (var field in fields)
            if (source[field] is { } value)
                confirmation[field] = value.DeepClone();
        return JsonSerializer.SerializeToElement(confirmation);
    }
}