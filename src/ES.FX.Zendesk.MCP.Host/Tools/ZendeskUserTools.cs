using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for Zendesk users. Namespaced <c>users_*</c> to mirror the Zendesk API structure.
/// </summary>
[McpServerToolType]
public sealed class ZendeskUserTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Returns the Zendesk user associated with the configured credentials.</summary>
    [McpServerTool(Name = "users_me_get", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the Zendesk user associated with the configured API credentials (the authenticated account). " +
        "Use to verify connectivity and identity. Read-only; makes no changes.")]
    public Task<ZendeskUser> Whoami(CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Users.GetCurrentUserAsync(cancellationToken));

    /// <summary>Returns a Zendesk user by id.</summary>
    [McpServerTool(Name = "users_get", ReadOnly = true, OpenWorld = true)]
    [Description("Returns a single Zendesk user by numeric id. Read-only.")]
    public Task<ZendeskUser> Read(
        [Description("The numeric Zendesk user id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Users.GetByIdAsync(id, cancellationToken));

    /// <summary>Searches Zendesk users.</summary>
    [McpServerTool(Name = "users_search", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Searches Zendesk users. The query matches name, email, phone, external id, and supports filters such as " +
        "\"role:agent\" or \"email:jane@example.com\". Returns a page of users plus the total count. Read-only.")]
    public Task<ZendeskUsersResult> Search(
        [Description(
            "The user search query using Zendesk search syntax. A bare term does a partial/full match on name, " +
            "email, notes, or phone (e.g. \"jdoe\"). Use property filters for precision: " +
            "role:end-user|agent|admin (or a custom role name), email:jane@example.com, group:\"Level 2\", " +
            "organization:mondocam, tags:premium, created<2011-05-01. Prefix a term with '-' to exclude, and use " +
            "type:user to force a user search.")]
        string query,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100). Offset pagination only, and no more than 10,000 total records " +
            "per query (paging past that returns an error) — narrow the query rather than paging deeper. The total " +
            "match count is in 'count'.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Users.SearchAsync(query, page, perPage, cancellationToken));

    /// <summary>Returns many users by id in one call (batch resolution).</summary>
    [McpServerTool(Name = "users_get_many", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns many Zendesk users by id in one call — use to resolve the requester/assignee/author/CC ids " +
        "found on tickets, comments, and audits to names/emails without one call per id. Lists larger than 100 ids " +
        "are fetched in batches of 100 automatically. Read-only.")]
    public Task<ZendeskUsersResult> ReadMany(
        [Description(
            "The numeric Zendesk user ids to resolve. The underlying Zendesk endpoint accepts at most 100 ids per " +
            "call; larger lists are fetched in batches of 100 automatically.")]
        long[] ids,
        [Description(
            "Sideloads to resolve inline as sibling arrays (merged and de-duplicated across batches): any of " +
            "\"organizations\", \"groups\", \"identities\".")]
        string[]? include = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Users.GetManyAsync(ids, include: include, cancellationToken: cancellationToken));

    /// <summary>Returns the tickets a user has requested (their ticket history).</summary>
    [McpServerTool(Name = "users_tickets_requested_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the tickets requested by a user — their support history. Useful context: prior issues, repeat " +
        "complaints, what was already tried, and whether the current issue is a reopen or duplicate. Read-only.")]
    public Task<ZendeskTicketsResult> RequestedTickets(
        [Description("The numeric Zendesk user id (the requester).")]
        long userId,
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
            zendeskApiClient.Users.GetRequestedTicketsAsync(userId, page, perPage, include, cancellationToken));

    /// <summary>Lists Zendesk users, optionally filtered by role.</summary>
    [McpServerTool(Name = "users_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists Zendesk users, optionally filtered by role (\"end-user\", \"agent\", or \"admin\"). Prefer " +
        "users_search or users_autocomplete when looking for a specific person. Cursor pagination: " +
        "pass pageSize/afterCursor; the result's meta.has_more/meta.after_cursor drive continuation. Read-only.")]
    public Task<ZendeskUsersResult> List(
        [Description(
            "Optional role filter. Allowed values are \"end-user\", \"agent\", \"admin\", or a custom role name.")]
        string? role = null,
        [Description("The cursor page size (max 100).")]
        int? pageSize = null,
        [Description("The cursor from the previous page's meta.after_cursor (optional).")]
        string? afterCursor = null,
        [Description(
            "Sideloads to resolve ids inline in one call: any of \"organizations\", \"groups\", \"identities\". Returned as sibling arrays.")]
        string[]? include = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Users.ListAsync(role, pageSize, afterCursor, include: include,
                cancellationToken: cancellationToken));

    /// <summary>Returns the (cached, approximate) user count, optionally filtered by role.</summary>
    [McpServerTool(Name = "users_count", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the user count, optionally filtered by role (\"end-user\", \"agent\", \"admin\", or a custom role " +
        "name). The count is approximate: if it exceeds 100,000 it is recomputed roughly every 24 hours, its value " +
        "is capped at 100,000 until the refresh completes, and refreshed_at may be null while Zendesk recomputes in " +
        "the background. Read-only.")]
    public Task<ZendeskCount> Count(
        [Description(
            "Optional role filter. Allowed values are \"end-user\", \"agent\", \"admin\", or a custom role name.")]
        string? role = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Users.CountAsync(role, cancellationToken: cancellationToken));

    /// <summary>Suggests users whose name starts with a prefix.</summary>
    [McpServerTool(Name = "users_autocomplete", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Suggests Zendesk users whose name starts with a prefix — a cheap " +
        "type-ahead lookup when you have a partial name. Offset-paginated only: count/next_page indicate more pages. " +
        "Read-only.")]
    public Task<ZendeskUsersResult> Autocomplete(
        [Description(
            "The name value to match. Matches users whose name STARTS WITH this value (a prefix match, not a " +
            "substring). Only returns users with no foreign identities.")]
        string name,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100). The total is in 'count'; a non-null 'next_page' means more pages.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Users.AutocompleteAsync(name, page, perPage, cancellationToken));

    /// <summary>Returns a user's related ticket/subscription counts.</summary>
    [McpServerTool(Name = "users_related_get", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns a user's related information — counts of requested/assigned tickets and subscriptions. A quick " +
        "gauge of how active a user is before pulling their full ticket lists. Read-only.")]
    public Task<ZendeskUserRelated> Related(
        [Description("The numeric Zendesk user id.")]
        long userId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Users.GetRelatedInformationAsync(userId, cancellationToken));

    /// <summary>Lists a user's identities (e-mails, phone numbers, social handles).</summary>
    [McpServerTool(Name = "users_identities_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists a user's identities — e-mail addresses, phone numbers, and social handles, with primary/verified " +
        "flags. Use to see all the contact points behind a user record. Cursor pagination: pass pageSize/afterCursor; " +
        "the result's meta.has_more/meta.after_cursor drive continuation. Read-only.")]
    public Task<ZendeskUserIdentitiesResult> Identities(
        [Description("The numeric Zendesk user id.")]
        long userId,
        [Description("The cursor page size (max 100).")]
        int? pageSize = null,
        [Description("The cursor from the previous page's meta.after_cursor (optional).")]
        string? afterCursor = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Users.GetIdentitiesAsync(userId, pageSize, afterCursor, cancellationToken));

    /// <summary>Lists the groups an agent belongs to.</summary>
    [McpServerTool(Name = "users_groups_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists the groups an agent belongs to — the teams their tickets can be routed through. Complements " +
        "groups_memberships_list (group → agents). count/next_page indicate more pages. Read-only.")]
    public Task<ZendeskGroupsResult> Groups(
        [Description("The numeric Zendesk user id (an agent).")]
        long userId,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 100, max 100). The total is in 'count'; a non-null 'next_page' means more pages.")]
        int? perPage = 100,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Users.GetGroupsAsync(userId, page, perPage, cancellationToken));

    /// <summary>Lists the organizations a user belongs to.</summary>
    [McpServerTool(Name = "users_organizations_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists the organizations a user belongs to — the companies/accounts their tickets are attributed to. " +
        "count/next_page indicate more pages. Read-only.")]
    public Task<ZendeskOrganizationsResult> Organizations(
        [Description("The numeric Zendesk user id.")]
        long userId,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 100, max 100). The total is in 'count'; a non-null 'next_page' means more pages.")]
        int? perPage = 100,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Users.GetOrganizationsAsync(userId, page, perPage, cancellationToken));

    /// <summary>Returns the tickets assigned to an agent.</summary>
    [McpServerTool(Name = "users_tickets_assigned_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the tickets assigned to an agent — their current and past workload. count/next_page indicate more " +
        "pages. Read-only.")]
    public Task<ZendeskTicketsResult> AssignedTickets(
        [Description("The numeric Zendesk user id (the assignee agent).")]
        long userId,
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
            zendeskApiClient.Users.GetAssignedTicketsAsync(userId, page, perPage, include, cancellationToken));

    /// <summary>Returns the tickets a user is CC'd on.</summary>
    [McpServerTool(Name = "users_tickets_ccd_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the tickets a user is CC'd on — issues they follow without being the requester or assignee. " +
        "count/next_page indicate more pages. Read-only.")]
    public Task<ZendeskTicketsResult> CcdTickets(
        [Description("The numeric Zendesk user id (the CC'd user).")]
        long userId,
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
            zendeskApiClient.Users.GetCcdTicketsAsync(userId, page, perPage, include, cancellationToken));

    /// <summary>Lists a user's tags.</summary>
    [McpServerTool(Name = "users_tags_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists a user's tags as plain strings. Requires user tagging to be enabled in Zendesk Support. Read-only.")]
    public Task<ZendeskTagNamesResult> Tags(
        [Description("The numeric Zendesk user id.")]
        long userId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Users.GetTagsAsync(userId, cancellationToken));
}