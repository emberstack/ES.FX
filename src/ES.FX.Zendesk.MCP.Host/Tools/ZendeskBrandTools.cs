using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP read tools for Zendesk brands (multibrand accounts). Namespaced <c>brands_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskBrandTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Lists Zendesk brands.</summary>
    [McpServerTool(Name = "brands_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists Zendesk brands — decodes the brand_id carried on tickets in multibrand accounts. Cursor " +
        "pagination: pass pageSize/afterCursor; the result's meta.has_more/meta.after_cursor drive continuation. " +
        "Read-only.")]
    public Task<ZendeskBrandsResult> List(
        [Description("The cursor page size (optional, max 100).")]
        int? pageSize = null,
        [Description("The cursor from the previous page's meta.after_cursor (omit for the first page).")]
        string? afterCursor = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Brands.ListAsync(pageSize: pageSize, afterCursor: afterCursor,
                cancellationToken: cancellationToken));

    /// <summary>Returns a Zendesk brand by id.</summary>
    [McpServerTool(Name = "brands_get", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns a single Zendesk brand by id — the name/subdomain behind a ticket's brand_id. Read-only.")]
    public Task<ZendeskBrand> Read(
        [Description("The numeric Zendesk brand id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Brands.GetByIdAsync(id, cancellationToken));
}
