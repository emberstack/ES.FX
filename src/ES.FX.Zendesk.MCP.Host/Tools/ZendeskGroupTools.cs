using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for Zendesk groups (agent teams tickets are routed to). Namespaced <c>zendesk_groups_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskGroupTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Lists Zendesk groups.</summary>
    [McpServerTool(Name = "zendesk_groups_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists Zendesk groups (agent teams). Use to resolve the numeric group_id on a ticket or organization to a " +
        "human-readable group name, or to see how support is organized. Read-only.")]
    public Task<ZendeskGroupsResult> List(
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 100, max 100). The total is in 'count'; a non-null 'next_page' means more pages.")]
        int? perPage = 100,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Groups.ListAsync(page, perPage, cancellationToken));

    /// <summary>Returns a Zendesk group by id.</summary>
    [McpServerTool(Name = "zendesk_groups_read", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns a single Zendesk group by id — the name/description behind a ticket's or organization's group_id. " +
        "Read-only.")]
    public Task<ZendeskGroup> Read(
        [Description("The numeric Zendesk group id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Groups.GetByIdAsync(id, cancellationToken));

    /// <summary>Lists the agents that belong to a group.</summary>
    [McpServerTool(Name = "zendesk_groups_memberships", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists the memberships (agents) of a group — the agents a ticket routed to this group can be assigned to. " +
        "Each membership carries a user_id; resolve names with zendesk_users_read or zendesk_users_read_many. Read-only.")]
    public Task<ZendeskGroupMembershipsResult> Memberships(
        [Description("The numeric Zendesk group id.")]
        long groupId,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 100, max 100). The total is in 'count'; a non-null 'next_page' means more pages.")]
        int? perPage = 100,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Groups.GetMembershipsAsync(groupId, page, perPage, cancellationToken));
}