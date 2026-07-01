using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for Zendesk users. Namespaced <c>zendesk_users_*</c> to mirror the Zendesk API structure.
/// </summary>
[McpServerToolType]
public sealed class ZendeskUserTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Returns the Zendesk user associated with the configured credentials.</summary>
    [McpServerTool(Name = "zendesk_users_whoami", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the Zendesk user associated with the configured API credentials (the authenticated account). " +
        "Use to verify connectivity and identity. Read-only; makes no changes.")]
    public Task<ZendeskUser> Whoami(CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Users.GetCurrentUserAsync(cancellationToken));

    /// <summary>Returns a Zendesk user by id.</summary>
    [McpServerTool(Name = "zendesk_users_read", ReadOnly = true, OpenWorld = true)]
    [Description("Returns a single Zendesk user by numeric id. Read-only.")]
    public Task<ZendeskUser> Read(
        [Description("The numeric Zendesk user id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Users.GetByIdAsync(id, cancellationToken));

    /// <summary>Searches Zendesk users.</summary>
    [McpServerTool(Name = "zendesk_users_search", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Searches Zendesk users. The query matches name, email, phone, external id, and supports filters such as " +
        "\"role:agent\" or \"email:jane@example.com\". Returns a page of users plus the total count. Read-only.")]
    public Task<ZendeskUsersResult> Search(
        [Description("The user search query, e.g. \"jane@example.com\" or \"role:admin\".")]
        string query,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100). Use 'page' to fetch more; the total match count is in 'count'.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Users.SearchAsync(query, page, perPage, cancellationToken));

    /// <summary>Returns many users by id in one call (batch resolution).</summary>
    [McpServerTool(Name = "zendesk_users_read_many", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns many Zendesk users by id in one call — use to resolve the requester/assignee/author/CC ids " +
        "found on tickets, comments, and audits to names/emails without one call per id. Lists larger than 100 ids " +
        "are fetched in batches of 100 automatically. Read-only.")]
    public Task<ZendeskUsersResult> ReadMany(
        [Description("The numeric Zendesk user ids to resolve.")]
        long[] ids,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Users.GetManyAsync(ids, cancellationToken));

    /// <summary>Returns the tickets a user has requested (their ticket history).</summary>
    [McpServerTool(Name = "zendesk_users_requested_tickets", ReadOnly = true, OpenWorld = true)]
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
}