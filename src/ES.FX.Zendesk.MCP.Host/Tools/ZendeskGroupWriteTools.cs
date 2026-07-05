using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP write tools for Zendesk groups and group memberships. Namespaced <c>groups_*</c>. Every tool
///     honors the server execution mode via <see cref="ZendeskToolInvoker.InvokeWriteAsync{T}" />.
/// </summary>
[McpServerToolType]
public sealed class ZendeskGroupWriteTools(
    IZendeskClient zendeskApiClient,
    IMcpExecutionModeAccessor executionMode)
{
    /// <summary>Creates a Zendesk group.</summary>
    [McpServerTool(Name = "groups_create", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Creates a Zendesk group (agent team). Decide 'is_public' at creation time — a private group can never be " +
        "made public later. Returns the created group. Write operation — honors the server execution mode: " +
        "rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Create(
        [Description(
            "The group to create. 'name' is required. Set 'is_public' to false for a private group — this cannot " +
            "be reversed later.")]
        ZendeskGroupWrite group,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create group '{group.Name}'",
            () => zendeskApiClient.Groups.CreateAsync(group, cancellationToken: cancellationToken),
            group);

    /// <summary>Updates a Zendesk group by id.</summary>
    [McpServerTool(Name = "groups_update", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = true)]
    [Description(
        "Updates a Zendesk group by id. Only the fields set in the payload change. A private group cannot be made " +
        "public. Returns the updated group. Write operation — honors the server execution mode: rejected in " +
        "read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Update(
        [Description("The numeric Zendesk group id.")]
        long id,
        [Description("The fields to change.")]
        ZendeskGroupWrite group,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"update group {id}",
            () => zendeskApiClient.Groups.UpdateAsync(id, group, cancellationToken: cancellationToken),
            new { id, group });

    /// <summary>Soft-deletes a Zendesk group by id.</summary>
    [McpServerTool(Name = "groups_delete", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = true)]
    [Description(
        "Soft-deletes a Zendesk group by id. Returns a completion acknowledgement. Write operation — honors the " +
        "server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Delete(
        [Description("The numeric Zendesk group id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"delete group {id}",
            () => zendeskApiClient.Groups.DeleteAsync(id, cancellationToken: cancellationToken),
            new { id });

    /// <summary>Assigns an agent to a group.</summary>
    [McpServerTool(Name = "groups_memberships_create", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Assigns a Zendesk agent to a group by creating a group membership. Set makeDefault so tickets assigned " +
        "directly to the agent assume this group. Returns the created membership. Write operation — honors the " +
        "server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> MembershipsCreate(
        [Description("The numeric Zendesk user id of the agent (must be an agent, not an end user).")]
        long userId,
        [Description("The numeric Zendesk group id.")]
        long groupId,
        [Description(
            "Whether this becomes the agent's default group — tickets assigned directly to the agent assume it " +
            "(optional).")]
        bool? makeDefault = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"assign user {userId} to group {groupId}",
            () => zendeskApiClient.Groups.CreateMembershipAsync(userId, groupId,
                @default: makeDefault, cancellationToken: cancellationToken),
            new { userId, groupId, makeDefault });

    /// <summary>Assigns up to 100 agents to groups as an async job.</summary>
    [McpServerTool(Name = "groups_memberships_create_many", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Assigns up to 100 Zendesk agents to groups in a single call. Each item needs 'user_id' and 'group_id'. " +
        "Returns a job_status — poll job_statuses_get until completed. Write operation — honors the " +
        "server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> MembershipsCreateMany(
        [Description(
            "The memberships to create (1-100). Each item needs an agent 'user_id' and a 'group_id'.")]
        ZendeskGroupMembership[] memberships,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create {memberships.Length} group memberships",
            () => zendeskApiClient.Groups.CreateManyMembershipsAsync(memberships,
                cancellationToken: cancellationToken),
            new { memberships });

    /// <summary>Removes a group membership by its membership id.</summary>
    [McpServerTool(Name = "groups_memberships_delete", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = true)]
    [Description(
        "Removes a Zendesk group membership by its MEMBERSHIP id (not the user or group id — list them with " +
        "groups_memberships_list). Side effect: Zendesk schedules a job un-assigning the agent's working " +
        "tickets in that group. Returns a completion acknowledgement. Write operation — honors the server " +
        "execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> MembershipsDelete(
        [Description(
            "The numeric group membership id (not a user or group id — get it from groups_memberships_list).")]
        long membershipId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"delete group membership {membershipId}",
            () => zendeskApiClient.Groups.DeleteMembershipAsync(membershipId,
                cancellationToken: cancellationToken),
            new { membershipId });

    /// <summary>Removes up to 100 group memberships as an async job.</summary>
    [McpServerTool(Name = "groups_memberships_delete_many", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Removes up to 100 Zendesk group memberships by their MEMBERSHIP ids. Side effect: Zendesk schedules a job " +
        "un-assigning the agents' working tickets in those groups. Returns a job_status — poll job_statuses_get " +
        "until completed. Write operation — honors the server execution mode: rejected in read-only mode, " +
        "simulated (no changes made) in dry-run mode.")]
    public Task<object> MembershipsDeleteMany(
        [Description(
            "The numeric group membership ids to remove (1-100; not user or group ids — get them from " +
            "groups_memberships_list).")]
        long[] membershipIds,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"delete {membershipIds.Length} group memberships",
            () => zendeskApiClient.Groups.DeleteManyMembershipsAsync(membershipIds,
                cancellationToken: cancellationToken),
            new { membershipIds });

    /// <summary>Makes a group membership the agent's default.</summary>
    [McpServerTool(Name = "groups_memberships_make_default", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = true)]
    [Description(
        "Makes a group membership the agent's default group. Returns the user's FULL group membership list (the " +
        "affected one has default=true). Write operation — honors the server execution mode: rejected in " +
        "read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> MembershipsMakeDefault(
        [Description("The numeric Zendesk user id owning the membership.")]
        long userId,
        [Description(
            "The numeric group membership id to make default (not a user or group id; must belong to the given " +
            "userId).")]
        long membershipId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"make group membership {membershipId} the default for user {userId}",
            () => zendeskApiClient.Groups.MakeMembershipDefaultAsync(userId, membershipId,
                cancellationToken: cancellationToken),
            new { userId, membershipId });
}
