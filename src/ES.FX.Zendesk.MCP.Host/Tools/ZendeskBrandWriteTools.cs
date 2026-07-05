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
///     MCP write tools for Zendesk brands (multibrand accounts). Namespaced <c>brands_*</c>.
/// </summary>
/// <remarks>
///     Requests are built through the generated request builders and sent as raw wire JSON
///     (<see cref="ZendeskKiotaRequests.SendForJsonAsync" />) rather than round-tripped through the generated
///     models: the published spec marks server-assigned fields (brand <c>id</c>, <c>url</c>,
///     <c>created_at</c>/<c>updated_at</c>, <c>ticket_form_ids</c>, ...) as read-only, so Kiota's serializer
///     would silently drop them. Write responses collapse to lean confirmations: create returns
///     <c>{id, name, subdomain, created_at}</c>; update returns <c>{id, updated_at}</c> plus the server-state
///     values of exactly the fields the request carried (echo-of-change — a value differing from the request
///     reveals a business-rule override without a follow-up <c>brands_get</c>).
/// </remarks>
[McpServerToolType]
public sealed class ZendeskBrandWriteTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IMcpExecutionModeAccessor executionMode)
{
    /// <summary>Creates a Zendesk brand.</summary>
    [McpServerTool(Name = "brands_create", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Create a Zendesk brand (admin-only account config). Returns {id,name,subdomain,created_at}; brands_get for full record. " +
        "Write op honors server execution mode: rejected in read-only, simulated (no change) in dry-run.")]
    public Task<object> Create(
        [Description(
            "name+subdomain required; optional active, default, brand_url, host_mapping, signature_template.")]
        ZendeskBrandWrite brand,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"create brand '{brand.Name}'",
            async () =>
            {
                var request = zendesk.Api.V2.Brands.ToPostRequestInformation(
                    new BrandCreateRequest { Brand = Map(brand) });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildConfirmation(json, "create", "id", "name", "subdomain", "created_at");
            },
            brand);

    /// <summary>Updates a Zendesk brand by id.</summary>
    [McpServerTool(Name = "brands_update", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Update a Zendesk brand by id (admin-only account config). Returns {id,updated_at} plus server-state of exactly the fields sent " +
        "— a value differing from your request reveals a business-rule override without a follow-up brands_get. " +
        "Write op honors server execution mode: rejected in read-only, simulated (no change) in dry-run.")]
    public Task<object> Update(
        [Description("Numeric Zendesk brand id.")]
        long id,
        [Description(
            "Fields to change: name, subdomain, active, default, brand_url, host_mapping, signature_template.")]
        ZendeskBrandWrite brand,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"update brand {id}",
            async () =>
            {
                var request = zendesk.Api.V2.Brands[id].ToPutRequestInformation(
                    new BrandUpdateRequest { Brand = Map(brand) });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildConfirmation(json, "update", ["id", "updated_at", .. RequestedFields(brand)]);
            },
            new { id, brand });

    /// <summary>Soft-deletes a Zendesk brand by id.</summary>
    [McpServerTool(Name = "brands_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Soft-delete a Zendesk brand by id (admin-only, account-wide config change). If it's the account default, make another brand default first (brands_update default=true). " +
        "Returns a completion ack carrying the structured brand id. " +
        "Write op honors server execution mode: rejected in read-only, simulated (no change) in dry-run.")]
    public Task<object> Delete(
        [Description("Numeric Zendesk brand id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"delete brand {id}",
            // The shared bodyless overload cannot know the record id, so the acknowledgement is built here —
            // the structured id lets the agent chain the result without parsing it out of prose.
            async () =>
            {
                await zendesk.Api.V2.Brands[id].DeleteAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return new ZendeskWriteAcknowledgement
                {
                    Description = $"Zendesk accepted the request to delete brand {id}.",
                    Id = id
                };
            }, new { id });

    /// <summary>
    ///     Maps the curated write model onto the generated request payload. Kiota omits unassigned properties on
    ///     the wire, matching the retired client's omit-null serialization.
    /// </summary>
    private static BrandObject Map(ZendeskBrandWrite brand) => new()
    {
        Name = brand.Name,
        Subdomain = brand.Subdomain,
        Active = brand.Active,
        Default = brand.Default,
        BrandUrl = brand.BrandUrl,
        HostMapping = brand.HostMapping,
        SignatureTemplate = brand.SignatureTemplate
    };

    /// <summary>
    ///     The wire names of the fields present on the request — the update confirmation's echo-of-change set
    ///     (the server-state value of each requested field is echoed back, revealing overrides).
    /// </summary>
    private static IEnumerable<string> RequestedFields(ZendeskBrandWrite brand)
    {
        if (brand.Name is not null) yield return "name";
        if (brand.Subdomain is not null) yield return "subdomain";
        if (brand.Active is not null) yield return "active";
        if (brand.Default is not null) yield return "default";
        if (brand.BrandUrl is not null) yield return "brand_url";
        if (brand.HostMapping is not null) yield return "host_mapping";
        if (brand.SignatureTemplate is not null) yield return "signature_template";
    }

    /// <summary>
    ///     Collapses a write response to the lean confirmation the tool returns: the named fields of the
    ///     response's <c>brand</c> member, in order, with null/absent fields omitted (absent = null/empty).
    /// </summary>
    private static JsonElement BuildConfirmation(JsonElement response, string action, params string[] fields)
    {
        if (response.ValueKind is not JsonValueKind.Object || !response.TryGetProperty("brand", out var brand) ||
            brand.ValueKind is not JsonValueKind.Object)
            throw new McpException(
                $"Zendesk returned an unexpected response for the brand {action} — the change may still have " +
                "been applied; verify with brands_get.");

        var source = (JsonObject)JsonNode.Parse(brand.GetRawText())!;
        var confirmation = new JsonObject();
        foreach (var field in fields)
            if (source[field] is { } value)
                confirmation[field] = value.DeepClone();
        return JsonSerializer.SerializeToElement(confirmation);
    }
}