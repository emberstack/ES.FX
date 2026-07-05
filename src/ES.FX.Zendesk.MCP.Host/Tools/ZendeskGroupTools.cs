using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for Zendesk groups (agent teams tickets are routed to). Namespaced <c>groups_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskGroupTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Lists Zendesk groups.</summary>
    [McpServerTool(Name = "groups_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists Zendesk groups (agent teams). Use to resolve the numeric group_id on a ticket or organization to a " +
        "human-readable group name, or to see how support is organized. Read-only.")]
    public Task<ZendeskGroupsResult> List(
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 100, max 100). The total is in 'count'; a non-null 'next_page' means more pages.")]
        int? perPage = 100,
        [Description(
            "Comma-separated sideloads to resolve inline as sibling arrays: \"users\" returns each group's members " +
            "in the same roundtrip, avoiding per-group groups_memberships_list calls. Documented values are " +
            "\"users\" and \"group_settings\"; there is no closed enum for group sideloads, so unknown values are " +
            "silently ignored.")]
        string[]? include = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Groups.ListAsync(page, perPage, include: include, cancellationToken: cancellationToken));

    /// <summary>Returns a Zendesk group by id.</summary>
    [McpServerTool(Name = "groups_get", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns a single Zendesk group by id — the name/description behind a ticket's or organization's group_id. " +
        "Read-only.")]
    public Task<ZendeskGroup> Read(
        [Description("The numeric Zendesk group id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Groups.GetByIdAsync(id, cancellationToken));

    /// <summary>Lists the agents that belong to a group.</summary>
    [McpServerTool(Name = "groups_memberships_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists the memberships (agents) of a group — the agents a ticket routed to this group can be assigned to. " +
        "Each membership carries a user_id; prefer the \"users\" sideload (include) to resolve names in this call " +
        "instead of following up with users_get_many. Read-only.")]
    public Task<ZendeskGroupMembershipsResult> Memberships(
        [Description("The numeric Zendesk group id.")]
        long groupId,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 100, max 100). The total is in 'count'; a non-null 'next_page' means more pages.")]
        int? perPage = 100,
        [Description(
            "Comma-separated sideloads to resolve ids inline in one call; valid values are \"users\" and " +
            "\"groups\". Returned as sibling arrays.")]
        string[]? include = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Groups.GetMembershipsAsync(groupId, page, perPage, include: include,
                cancellationToken: cancellationToken));

    /// <summary>Lists the groups assignable to tickets for the current agent.</summary>
    [McpServerTool(Name = "groups_assignable_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists the Zendesk groups assignable to tickets for the authenticated agent — use it to pick a valid " +
        "group_id when routing a ticket, instead of guessing from groups_list. Offset pagination: " +
        "'count'/'next_page' indicate more pages. Read-only.")]
    public Task<ZendeskGroupsResult> Assignable(
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 100, max 100). The total is in 'count'; a non-null 'next_page' means more pages.")]
        int? perPage = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Groups.GetAssignableAsync(page, perPage, cancellationToken: cancellationToken));

    /// <summary>Returns the approximate group count.</summary>
    [McpServerTool(Name = "groups_count", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the number of Zendesk groups. The value is cached and approximate above 100,000: it is refreshed " +
        "only every ~24 hours and 'value' is capped at 100,000 until that refresh completes ('refreshed_at' reports " +
        "the cache time and may be null while Zendesk recomputes). Read-only.")]
    public Task<ZendeskCount> Count(CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Groups.CountAsync(cancellationToken));

    /// <summary>Lists the users of a group.</summary>
    [McpServerTool(Name = "groups_users_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists the users (agents) of a Zendesk group as full user records — a one-call alternative to " +
        "groups_memberships_list followed by users_get_many when only the users matter. Offset " +
        "pagination: 'count'/'next_page' indicate more pages. Read-only.")]
    public Task<ZendeskUsersResult> Users(
        [Description("The numeric Zendesk group id.")]
        long groupId,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 100, max 100). The total is in 'count'; a non-null 'next_page' means more pages.")]
        int? perPage = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Groups.GetUsersAsync(groupId, page, perPage, cancellationToken: cancellationToken));
}