using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP write tools for Zendesk organizations and organization memberships. Namespaced
///     <c>organizations_*</c>. Every tool honors the server execution mode via
///     <see cref="ZendeskToolInvoker.InvokeWriteAsync{T}" />.
/// </summary>
[McpServerToolType]
public sealed class ZendeskOrganizationWriteTools(
    IZendeskClient zendeskApiClient,
    IMcpExecutionModeAccessor executionMode)
{
    /// <summary>Creates a Zendesk organization.</summary>
    [McpServerTool(Name = "organizations_create", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Creates a Zendesk organization. The name must be unique across the account. Returns the created " +
        "organization. Write operation — honors the server execution mode: rejected in read-only mode, simulated " +
        "(no changes made) in dry-run mode.")]
    public Task<object> Create(
        [Description("The organization to create. 'name' is required and must be unique.")]
        ZendeskOrganizationWrite organization,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create organization '{organization.Name}'",
            () => zendeskApiClient.Organizations.CreateAsync(organization, cancellationToken: cancellationToken),
            organization);

    /// <summary>Creates up to 100 Zendesk organizations as an async job.</summary>
    [McpServerTool(Name = "organizations_create_many", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Creates up to 100 Zendesk organizations in a single call. Returns a job_status — poll " +
        "job_statuses_get until completed; per-item outcomes are in the job's results. Write operation — " +
        "honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> CreateMany(
        [Description("The organizations to create (1-100). Each 'name' must be unique.")]
        ZendeskOrganizationWrite[] organizations,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create {organizations.Length} organizations",
            () => zendeskApiClient.Organizations.CreateManyAsync(organizations,
                cancellationToken: cancellationToken),
            new { organizations });

    /// <summary>Creates or updates a Zendesk organization, matching by id or external id.</summary>
    [McpServerTool(Name = "organizations_create_or_update", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = true)]
    [Description(
        "Creates or updates a Zendesk organization. Matching uses 'id' or 'external_id' — NOT the name; sending " +
        "an existing name without a matching key errors. Returns the created or updated organization. Write " +
        "operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in " +
        "dry-run mode.")]
    public Task<object> CreateOrUpdate(
        [Description(
            "The organization to create or update. Set 'id' or 'external_id' to update an existing organization.")]
        ZendeskOrganizationWrite organization,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create or update organization '{organization.Name}'",
            () => zendeskApiClient.Organizations.CreateOrUpdateAsync(organization,
                cancellationToken: cancellationToken),
            organization);

    /// <summary>Updates a Zendesk organization by id.</summary>
    [McpServerTool(Name = "organizations_update", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = true)]
    [Description(
        "Updates a Zendesk organization by id. Only the fields set in the payload change — except 'domain_names', " +
        "which OVERWRITES the existing list, so always send the complete list. Returns the updated organization. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes " +
        "made) in dry-run mode.")]
    public Task<object> Update(
        [Description("The numeric Zendesk organization id.")]
        long id,
        [Description("The fields to change. 'domain_names' overwrites — send the complete list.")]
        ZendeskOrganizationWrite organization,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"update organization {id}",
            () => zendeskApiClient.Organizations.UpdateAsync(id, organization,
                cancellationToken: cancellationToken),
            new { id, organization });

    /// <summary>Applies the same change to up to 100 organizations as an async job.</summary>
    [McpServerTool(Name = "organizations_update_many", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Applies the SAME change to up to 100 Zendesk organizations by id. For per-organization changes use " +
        "organizations_update_many_batch instead. Returns a job_status — poll job_statuses_get " +
        "until completed. Write operation — honors the server execution mode: rejected in read-only mode, " +
        "simulated (no changes made) in dry-run mode.")]
    public Task<object> UpdateMany(
        [Description("The numeric organization ids to update (1-100).")]
        long[] ids,
        [Description("The change applied to every listed organization.")]
        ZendeskOrganizationWrite change,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"update {ids.Length} organizations with the same change",
            () => zendeskApiClient.Organizations.UpdateManyAsync(ids, change,
                cancellationToken: cancellationToken),
            new { ids, change });

    /// <summary>Applies per-organization changes to up to 100 organizations as an async job.</summary>
    [McpServerTool(Name = "organizations_update_many_batch", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Applies PER-ORGANIZATION changes to up to 100 Zendesk organizations in a single call; every item must " +
        "carry its 'id'. For the same change across many ids use organizations_update_many instead. " +
        "Returns a job_status — poll job_statuses_get until completed. Write operation — honors the " +
        "server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> UpdateManyBatch(
        [Description("The per-organization changes (1-100). Every item must include 'id'.")]
        ZendeskOrganizationWrite[] organizations,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"update {organizations.Length} organizations (batch)",
            () => zendeskApiClient.Organizations.UpdateManyAsync(organizations,
                cancellationToken: cancellationToken),
            new { organizations });

    /// <summary>Deletes a Zendesk organization by id.</summary>
    [McpServerTool(Name = "organizations_delete", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = true)]
    [Description(
        "Deletes a Zendesk organization by id. PERMANENT — organizations have no soft-delete or restore; user and " +
        "ticket associations to the organization are removed and cannot be recovered. Returns a completion " +
        "acknowledgement. Write operation — honors the server execution mode: rejected in read-only mode, " +
        "simulated (no changes made) in dry-run mode.")]
    public Task<object> Delete(
        [Description("The numeric Zendesk organization id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"delete organization {id}",
            () => zendeskApiClient.Organizations.DeleteAsync(id, cancellationToken: cancellationToken),
            new { id });

    /// <summary>Deletes up to 100 Zendesk organizations as an async job.</summary>
    [McpServerTool(Name = "organizations_delete_many", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Deletes up to 100 Zendesk organizations by id. PERMANENT — organizations have no soft-delete or restore. " +
        "Returns a job_status — poll job_statuses_get until completed. Write operation — honors the " +
        "server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> DeleteMany(
        [Description("The numeric organization ids to delete (1-100).")]
        long[] ids,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"delete {ids.Length} organizations",
            () => zendeskApiClient.Organizations.DeleteManyAsync(ids, cancellationToken: cancellationToken),
            new { ids });

    /// <summary>Merges one Zendesk organization into another (irreversible).</summary>
    [McpServerTool(Name = "organizations_merge", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Merges a Zendesk organization INTO another: the loser organization is DELETED and its users, tickets and " +
        "domain names move to the winner. Irreversible; admin-only. The merge runs asynchronously but is NOT a " +
        "job_status — the returned organization_merge carries an opaque string id; poll " +
        "organizations_merges_get with it until status is 'complete'. Write operation — honors the " +
        "server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Merge(
        [Description("The id of the organization to merge and delete (the loser).")]
        long loserOrganizationId,
        [Description("The id of the organization that absorbs the loser (the winner).")]
        long winnerOrganizationId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"merge organizations: {loserOrganizationId} (loser) into {winnerOrganizationId} (winner)",
            () => zendeskApiClient.Organizations.MergeAsync(loserOrganizationId, winnerOrganizationId,
                cancellationToken: cancellationToken),
            new { loserOrganizationId, winnerOrganizationId });

    /// <summary>Links a user to an organization.</summary>
    [McpServerTool(Name = "organizations_memberships_create", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Links a Zendesk user to an organization by creating an organization membership. Fails with 422 if the " +
        "membership already exists. Returns the created membership. Write operation — honors the server execution " +
        "mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> MembershipsCreate(
        [Description("The numeric Zendesk user id.")]
        long userId,
        [Description("The numeric Zendesk organization id.")]
        long organizationId,
        [Description("Whether the new membership becomes the user's default organization (optional).")]
        bool? makeDefault = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"link user {userId} to organization {organizationId}",
            () => zendeskApiClient.Organizations.CreateMembershipAsync(userId, organizationId,
                @default: makeDefault, cancellationToken: cancellationToken),
            new { userId, organizationId, makeDefault });

    /// <summary>Creates up to 100 organization memberships as an async job.</summary>
    [McpServerTool(Name = "organizations_memberships_create_many", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Creates up to 100 Zendesk organization memberships (user-to-organization links) in a single call. Each " +
        "item needs 'user_id' and 'organization_id'. Returns a job_status — poll job_statuses_get until " +
        "completed. Write operation — honors the server execution mode: rejected in read-only mode, simulated (no " +
        "changes made) in dry-run mode.")]
    public Task<object> MembershipsCreateMany(
        [Description("The memberships to create (1-100). Each item needs 'user_id' and 'organization_id'.")]
        ZendeskOrganizationMembership[] memberships,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create {memberships.Length} organization memberships",
            () => zendeskApiClient.Organizations.CreateManyMembershipsAsync(memberships,
                cancellationToken: cancellationToken),
            new { memberships });

    /// <summary>Removes an organization membership by its membership id.</summary>
    [McpServerTool(Name = "organizations_memberships_delete", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = true)]
    [Description(
        "Removes a Zendesk organization membership by its MEMBERSHIP id (not the user or organization id — list " +
        "them with organizations_memberships_list). Side effect: Zendesk schedules a job un-assigning the " +
        "user's working tickets for that organization. Returns a completion acknowledgement. Write operation — " +
        "honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> MembershipsDelete(
        [Description("The numeric organization membership id.")]
        long membershipId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"delete organization membership {membershipId}",
            () => zendeskApiClient.Organizations.DeleteMembershipAsync(membershipId,
                cancellationToken: cancellationToken),
            new { membershipId });

    /// <summary>Removes up to 100 organization memberships as an async job.</summary>
    [McpServerTool(Name = "organizations_memberships_delete_many", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Removes up to 100 Zendesk organization memberships by their MEMBERSHIP ids. Returns a job_status — poll " +
        "job_statuses_get until completed. Write operation — honors the server execution mode: rejected " +
        "in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> MembershipsDeleteMany(
        [Description("The numeric organization membership ids to remove (1-100).")]
        long[] membershipIds,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"delete {membershipIds.Length} organization memberships",
            () => zendeskApiClient.Organizations.DeleteManyMembershipsAsync(membershipIds,
                cancellationToken: cancellationToken),
            new { membershipIds });

    /// <summary>Makes an organization membership the user's default.</summary>
    [McpServerTool(Name = "organizations_memberships_make_default", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = true)]
    [Description(
        "Makes an organization membership the user's default organization. Returns the user's FULL organization " +
        "membership list (the affected one has default=true). Write operation — honors the server execution mode: " +
        "rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> MembershipsMakeDefault(
        [Description("The numeric Zendesk user id owning the membership.")]
        long userId,
        [Description("The numeric organization membership id to make default.")]
        long membershipId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"make organization membership {membershipId} the default for user {userId}",
            () => zendeskApiClient.Organizations.MakeMembershipDefaultAsync(userId, membershipId,
                cancellationToken: cancellationToken),
            new { userId, membershipId });
}
