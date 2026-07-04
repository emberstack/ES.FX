using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP write tools for Zendesk brands (multibrand accounts). Namespaced <c>zendesk_brands_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskBrandWriteTools(IZendeskClient zendeskApiClient, IMcpExecutionModeAccessor executionMode)
{
    /// <summary>Creates a Zendesk brand.</summary>
    [McpServerTool(Name = "zendesk_brands_create", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Creates a Zendesk brand (admin-only account configuration). 'name' and 'subdomain' are required. Returns " +
        "the created brand. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Create(
        [Description(
            "The brand definition: name and subdomain (both required on create), plus optional active, default, " +
            "brand_url, host_mapping, and signature_template.")]
        ZendeskBrandWrite brand,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"create brand '{brand.Name}'",
            () => zendeskApiClient.Brands.CreateAsync(brand, cancellationToken: cancellationToken), brand);

    /// <summary>Updates a Zendesk brand by id.</summary>
    [McpServerTool(Name = "zendesk_brands_update", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Updates a Zendesk brand by id (admin-only account configuration). Returns the updated brand. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Update(
        [Description("The numeric Zendesk brand id.")]
        long id,
        [Description("The brand fields to change (name, subdomain, active, default, brand_url, host_mapping, signature_template).")]
        ZendeskBrandWrite brand,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"update brand {id}",
            () => zendeskApiClient.Brands.UpdateAsync(id, brand, cancellationToken: cancellationToken),
            new { id, brand });

    /// <summary>Soft-deletes a Zendesk brand by id.</summary>
    [McpServerTool(Name = "zendesk_brands_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Soft-deletes a Zendesk brand by id (admin-only, account-wide configuration change). If the brand is the " +
        "account default, make another brand the default first (zendesk_brands_update with default = true). " +
        "Returns a completion acknowledgement. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Delete(
        [Description("The numeric Zendesk brand id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"delete brand {id}",
            () => zendeskApiClient.Brands.DeleteAsync(id, cancellationToken: cancellationToken), new { id });
}
