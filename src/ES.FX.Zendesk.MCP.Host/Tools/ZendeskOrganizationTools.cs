using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for Zendesk organizations. Namespaced <c>organizations_*</c> to mirror the Zendesk API.
/// </summary>
[McpServerToolType]
public sealed class ZendeskOrganizationTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Returns a Zendesk organization by id.</summary>
    [McpServerTool(Name = "organizations_get", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns a Zendesk organization by id — the requester's account/company context: domains, custom org " +
        "fields (plan/tier/region), the default routing group, tags, and internal notes. Read-only.")]
    public Task<ZendeskOrganization> Read(
        [Description("The numeric Zendesk organization id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Organizations.GetByIdAsync(id, cancellationToken));

    /// <summary>Returns the tickets belonging to an organization.</summary>
    [McpServerTool(Name = "organizations_tickets_list", ReadOnly = true, OpenWorld = true)]
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

    /// <summary>Lists Zendesk organizations.</summary>
    [McpServerTool(Name = "organizations_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists Zendesk organizations — browse the accounts/companies known to the instance. Cursor pagination: pass " +
        "pageSize/afterCursor; the result's meta.has_more/meta.after_cursor drive continuation. For a lookup by " +
        "exact name or external id use organizations_get_by_name_or_external_id instead. Read-only.")]
    public Task<ZendeskOrganizationsResult> List(
        [Description("Results per page (default 100, max 100).")]
        int? pageSize = null,
        [Description("The cursor from the previous page's meta.after_cursor (optional; omit for the first page).")]
        string? afterCursor = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Organizations.ListAsync(pageSize, afterCursor, cancellationToken));

    /// <summary>Returns the approximate organization count.</summary>
    [McpServerTool(Name = "organizations_count", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the number of Zendesk organizations. The value is cached and approximate above 100,000 (refreshed " +
        "roughly every 24 hours; 'refreshed_at' reports the cache time and may be null while Zendesk recomputes). " +
        "Read-only.")]
    public Task<ZendeskCount> Count(CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Organizations.CountAsync(cancellationToken));

    /// <summary>Returns many Zendesk organizations by id.</summary>
    [McpServerTool(Name = "organizations_get_many", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns many Zendesk organizations by id in one call — the batch form of organizations_get, ideal " +
        "for resolving the organization_ids collected from tickets or users. Lists larger than 100 ids are chunked " +
        "automatically and merged. Read-only.")]
    public Task<ZendeskOrganizationsResult> ReadMany(
        [Description("The numeric Zendesk organization ids to fetch.")]
        long[] ids,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Organizations.GetManyAsync(ids, cancellationToken));

    /// <summary>Looks up organizations by exact name or external id.</summary>
    [McpServerTool(Name = "organizations_get_by_name_or_external_id", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Looks up Zendesk organizations by EXACT (case-insensitive) name or by external id. Provide exactly one of " +
        "'name' or 'externalId' — this is an exact-match lookup, not search syntax. For prefix matching use " +
        "organizations_autocomplete. Read-only.")]
    public Task<ZendeskOrganizationsResult> Search(
        [Description("The exact organization name (case-insensitive). Mutually exclusive with externalId.")]
        string? name = null,
        [Description("The organization's external id. Mutually exclusive with name.")]
        string? externalId = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Organizations.SearchAsync(name, externalId, cancellationToken));

    /// <summary>Suggests organizations whose name starts with a prefix.</summary>
    [McpServerTool(Name = "organizations_autocomplete", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Suggests Zendesk organizations whose name starts with the given prefix — use when the exact name is " +
        "unknown (organizations_get_by_name_or_external_id requires an exact match). Offset pagination only: " +
        "'count'/'next_page' indicate more pages. Read-only.")]
    public Task<ZendeskOrganizationsResult> Autocomplete(
        [Description("The name prefix to match (at least the first characters of the organization name).")]
        string name,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 100, max 100). The total is in 'count'; a non-null 'next_page' means more pages.")]
        int? perPage = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Organizations.AutocompleteAsync(name, page, perPage, cancellationToken));

    /// <summary>Lists the users of an organization.</summary>
    [McpServerTool(Name = "organizations_users_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists the users belonging to a Zendesk organization — the people (end users and agents) attached to the " +
        "account. Offset pagination: 'count'/'next_page' indicate more pages. Read-only.")]
    public Task<ZendeskUsersResult> Users(
        [Description("The numeric Zendesk organization id.")]
        long organizationId,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 100, max 100). The total is in 'count'; a non-null 'next_page' means more pages.")]
        int? perPage = null,
        [Description(
            "Sideloads to resolve ids inline in one call: any of \"organizations\", \"groups\", \"identities\". " +
            "Returned as sibling arrays.")]
        string[]? include = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Organizations.GetUsersAsync(organizationId, page, perPage, include: include,
                cancellationToken: cancellationToken));

    /// <summary>Lists an organization's memberships.</summary>
    [McpServerTool(Name = "organizations_memberships_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists a Zendesk organization's memberships — the user-to-organization links, each carrying its membership " +
        "id (needed to delete a link) and whether it is the user's default organization. For the full user records " +
        "use organizations_users_list instead. Offset pagination: 'count'/'next_page' indicate more pages. " +
        "Read-only.")]
    public Task<ZendeskOrganizationMembershipsResult> Memberships(
        [Description("The numeric Zendesk organization id.")]
        long organizationId,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 100, max 100). The total is in 'count'; a non-null 'next_page' means more pages.")]
        int? perPage = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Organizations.GetMembershipsAsync(organizationId, page, perPage, cancellationToken));

    /// <summary>Returns an organization merge job's status.</summary>
    [McpServerTool(Name = "organizations_merges_get", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the status of a Zendesk organization merge — poll after organizations_merge until 'status' " +
        "is \"complete\" (or \"error\"). Note the id is the merge's own string id, not a job_status id — " +
        "job_statuses_get does not track organization merges. Read-only.")]
    public Task<ZendeskOrganizationMerge> MergeStatus(
        [Description("The organization merge id (a string) returned by organizations_merge.")]
        string mergeId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Organizations.GetMergeAsync(mergeId,
            cancellationToken));

    /// <summary>Lists an organization's tags.</summary>
    [McpServerTool(Name = "organizations_tags_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists the tags set on a Zendesk organization (requires organization tagging to be enabled in Support). " +
        "Tags are changed via organizations_update by sending the full replacement list. Read-only.")]
    public Task<ZendeskTagNamesResult> Tags(
        [Description("The numeric Zendesk organization id.")]
        long organizationId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Organizations.GetTagsAsync(organizationId, cancellationToken));
}