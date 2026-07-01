using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for Zendesk organizations. Namespaced <c>zendesk_organizations_*</c> to mirror the Zendesk API.
/// </summary>
[McpServerToolType]
public sealed class ZendeskOrganizationTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Returns a Zendesk organization by id.</summary>
    [McpServerTool(Name = "zendesk_organizations_read", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns a Zendesk organization by id — the requester's account/company context: domains, custom org " +
        "fields (plan/tier/region), the default routing group, tags, and internal notes. Read-only.")]
    public Task<ZendeskOrganization> Read(
        [Description("The numeric Zendesk organization id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Organizations.GetByIdAsync(id, cancellationToken));

    /// <summary>Returns the tickets belonging to an organization.</summary>
    [McpServerTool(Name = "zendesk_organizations_tickets", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the tickets belonging to an organization — the account-wide ticket history, useful for spotting " +
        "recurring or systemic issues and ongoing incidents affecting the same company. Read-only.")]
    public Task<ZendeskTicketsResult> Tickets(
        [Description("The numeric Zendesk organization id.")]
        long organizationId,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100). The total is in 'count'; a non-null 'next_page' means more pages — advance 'page'.")]
        int? perPage = 25,
        [Description(
            "Sideloads to resolve ids inline in one call: any of \"users\", \"groups\", \"organizations\". Returned as sibling arrays.")]
        string[]? include = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Organizations.GetTicketsAsync(organizationId, page, perPage, include, cancellationToken));
}