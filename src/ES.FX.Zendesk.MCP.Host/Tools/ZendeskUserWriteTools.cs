using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP write tools for Zendesk users — create, update, merge, delete, and identity management. Namespaced
///     <c>users_*</c>. Every tool honors the server execution mode via
///     <see cref="ZendeskToolInvoker.InvokeWriteAsync{T}" />.
/// </summary>
[McpServerToolType]
public sealed class ZendeskUserWriteTools(IZendeskClient zendeskApiClient, IMcpExecutionModeAccessor executionMode)
{
    /// <summary>Creates a Zendesk user.</summary>
    [McpServerTool(Name = "users_create", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Creates a Zendesk user. On create, 'email' becomes the primary e-mail identity; a duplicate e-mail fails " +
        "with 422 — use users_create_or_update to upsert instead. Allowed roles are \"end-user\", \"agent\", or " +
        "\"admin\"; omitting the role creates an end user. Set skip_verify_email to suppress the verification " +
        "e-mail. Returns the created user. Write operation — honors the server execution mode: rejected in " +
        "read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Create(
        [Description("The user to create (name, email, role, phone, external_id, organization_id, tags, ...).")]
        ZendeskUserWrite user,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create user '{user.Name ?? user.Email ?? "(unnamed)"}'",
            () => zendeskApiClient.Users.CreateAsync(user, cancellationToken: cancellationToken), user);

    /// <summary>Creates or updates a Zendesk user matched by e-mail or external id (upsert).</summary>
    [McpServerTool(Name = "users_create_or_update", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Creates a Zendesk user, or updates the existing one matched by e-mail or external id (upsert). Safe way " +
        "to ensure a user exists without triggering the 422 duplicate-email failure of users_create. The " +
        "external_id match is case-insensitive, but the stored external_id is updated to the case you supply. " +
        "Returns 200 if the user already existed, 201 if created; a newly created user with no role becomes an end " +
        "user. Returns the created or updated user. Write operation — honors the server execution mode: rejected " +
        "in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> CreateOrUpdate(
        [Description("The user to create or update — matched to an existing user by email or external_id.")]
        ZendeskUserWrite user,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create or update user '{user.Name ?? user.Email ?? user.ExternalId ?? "(unspecified)"}'",
            () => zendeskApiClient.Users.CreateOrUpdateAsync(user, cancellationToken: cancellationToken), user);

    /// <summary>Creates up to 100 Zendesk users as an async job.</summary>
    [McpServerTool(Name = "users_create_many", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Creates up to 100 Zendesk users in one call as an async job. NOTE: bulk user imports are off by default — " +
        "Zendesk support must enable them for the account or the call returns 403. Returns a job_status — poll " +
        "job_statuses_get until completed. Write operation — honors the server execution mode: rejected " +
        "in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> CreateMany(
        [Description("The users to create (1-100 per call).")]
        ZendeskUserWrite[] users,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create {users.Length} users as an async job",
            () => zendeskApiClient.Users.CreateManyAsync(users, cancellationToken: cancellationToken), users);

    /// <summary>Creates or updates up to 100 Zendesk users as an async job.</summary>
    [McpServerTool(Name = "users_create_or_update_many", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Creates or updates up to 100 Zendesk users in one call as an async job — each item is matched to an " +
        "existing user by e-mail or external id (upsert). Same gating as users_create_many: bulk user " +
        "imports must be enabled by Zendesk support or the call returns 403. Returns a job_status — poll " +
        "job_statuses_get until completed. Write operation — honors the server execution mode: rejected " +
        "in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> CreateOrUpdateMany(
        [Description("The users to create or update (1-100 per call), matched by email or external_id.")]
        ZendeskUserWrite[] users,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create or update {users.Length} users as an async job",
            () => zendeskApiClient.Users.CreateOrUpdateManyAsync(users, cancellationToken: cancellationToken),
            users);

    /// <summary>Updates a Zendesk user by id.</summary>
    [McpServerTool(Name = "users_update", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Updates a Zendesk user by id; only the fields set in the request are changed. QUIRK: setting 'email' " +
        "here ADDS it as a secondary identity instead of changing the primary — use " +
        "users_identities_create + users_identities_make_primary to change the primary e-mail. " +
        "Returns the updated user. Write operation — honors the server execution mode: rejected in read-only " +
        "mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Update(
        [Description("The numeric Zendesk user id.")]
        long id,
        [Description("The fields to change; unset (null) fields are left untouched.")]
        ZendeskUserWrite user,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"update user {id}",
            () => zendeskApiClient.Users.UpdateAsync(id, user, cancellationToken: cancellationToken),
            new { id, user });

    /// <summary>Applies the same change to up to 100 Zendesk users as an async job.</summary>
    [McpServerTool(Name = "users_update_many", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Applies the SAME change to up to 100 Zendesk users as an async job — e.g. suspend a batch, retag, or " +
        "move users to an organization. For per-user changes use users_update_many_batch. Returns a " +
        "job_status — poll job_statuses_get until completed. Write operation — honors the server " +
        "execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> UpdateMany(
        [Description("The numeric Zendesk user ids to update (1-100 per call).")]
        long[] ids,
        [Description("The change applied to every listed user; unset (null) fields are left untouched.")]
        ZendeskUserWrite change,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"update {ids.Length} users with the same change",
            () => zendeskApiClient.Users.UpdateManyAsync(ids, change, cancellationToken: cancellationToken),
            new { ids, change });

    /// <summary>Applies per-user changes to up to 100 Zendesk users as an async job.</summary>
    [McpServerTool(Name = "users_update_many_batch", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Applies PER-USER changes to up to 100 Zendesk users as an async job — every item must carry its 'id'. " +
        "For applying one identical change to many users use users_update_many instead. Returns a " +
        "job_status — poll job_statuses_get until completed. Write operation — honors the server " +
        "execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> UpdateManyBatch(
        [Description("The per-user changes (1-100 per call); every item MUST include the 'id' of the user to update.")]
        ZendeskUserWrite[] users,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"update {users.Length} users with per-user changes",
            () => zendeskApiClient.Users.UpdateManyAsync(users, cancellationToken: cancellationToken), users);

    /// <summary>Merges one end user into another; the loser is absorbed and the winner survives.</summary>
    [McpServerTool(Name = "users_merge", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Merges one Zendesk end user INTO another: the user identified by loserUserId is ABSORBED (their tickets " +
        "and identities move to the winner and the loser ceases to exist as a separate user); the user identified " +
        "by winnerUserId survives. End users only — agents/admins cannot be merged, nor can end users created by " +
        "sharing agreements. The loser (the user being absorbed) must be a requester on 10,000 or fewer tickets or " +
        "the merge is blocked. This cannot be undone. Returns the surviving (winner) user. Write operation — " +
        "honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Merge(
        [Description("The id of the user to be absorbed (the LOSER — this user is merged away).")]
        long loserUserId,
        [Description("The id of the user that survives the merge (the WINNER).")]
        long winnerUserId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"merge users: {loserUserId} (loser) into {winnerUserId} (winner)",
            () => zendeskApiClient.Users.MergeAsync(loserUserId, winnerUserId,
                cancellationToken: cancellationToken),
            new { loserUserId, winnerUserId });

    /// <summary>Soft-deletes a Zendesk user.</summary>
    [McpServerTool(Name = "users_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Soft-deletes a Zendesk user. Documented by Zendesk as NOT recoverable; a GDPR purge additionally " +
        "requires users_delete_permanently afterwards. Returns the deleted user record. Write operation — " +
        "honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Delete(
        [Description("The numeric Zendesk user id to delete.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"delete user {id}",
            () => zendeskApiClient.Users.DeleteAsync(id, cancellationToken: cancellationToken), new { id });

    /// <summary>Soft-deletes up to 100 Zendesk users as an async job.</summary>
    [McpServerTool(Name = "users_delete_many", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Soft-deletes up to 100 Zendesk users in one call as an async job (admin-only). Documented by Zendesk as " +
        "NOT recoverable. Returns a job_status — poll job_statuses_get until completed. Write operation — " +
        "honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> DeleteMany(
        [Description("The numeric Zendesk user ids to delete (1-100 per call).")]
        long[] ids,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"delete {ids.Length} users",
            () => zendeskApiClient.Users.DeleteManyAsync(ids, cancellationToken: cancellationToken), new { ids });

    /// <summary>Permanently deletes an already soft-deleted Zendesk user. Irreversible.</summary>
    [McpServerTool(Name = "users_delete_permanently", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "PERMANENTLY deletes a Zendesk user that has ALREADY been soft-deleted (via users_delete or " +
        "users_delete_many) — it does not work on active users. IRREVERSIBLE; used for GDPR purges. " +
        "Zendesk enforces a dedicated rate limit of 700 permanent deletions per 10 minutes. Returns the deleted " +
        "user record. Write operation — honors the server execution mode: rejected in read-only mode, simulated " +
        "(no changes made) in dry-run mode.")]
    public Task<object> DeletePermanently(
        [Description("The numeric id of the ALREADY soft-deleted user to purge.")]
        long deletedUserId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"permanently delete already-deleted user {deletedUserId}",
            () => zendeskApiClient.Users.DeletePermanentlyAsync(deletedUserId,
                cancellationToken: cancellationToken),
            new { deletedUserId });

    /// <summary>Adds an identity (e-mail, phone, social handle) to a Zendesk user.</summary>
    [McpServerTool(Name = "users_identities_create", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Adds an identity (e-mail, phone number, social handle) to a Zendesk user. 'primary' is only writable at " +
        "creation time — to promote an existing identity use users_identities_make_primary. Returns the " +
        "created identity. Write operation — honors the server execution mode: rejected in read-only mode, " +
        "simulated (no changes made) in dry-run mode.")]
    public Task<object> IdentitiesCreate(
        [Description("The numeric Zendesk user id.")]
        long userId,
        [Description(
            "The identity to add (type, value, verified, primary, skip_verify_email). Allowed type: " +
            "email, phone_number, twitter, facebook, google, agent_forwarding (also any_channel, foreign, " +
            "sdk, messaging, microsoft).")]
        ZendeskUserIdentityWrite identity,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"add an identity to user {userId}",
            () => zendeskApiClient.Users.CreateIdentityAsync(userId, identity,
                cancellationToken: cancellationToken),
            new { userId, identity });

    /// <summary>Updates a Zendesk user identity's value or verification state.</summary>
    [McpServerTool(Name = "users_identities_update", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = true)]
    [Description(
        "Updates a Zendesk user identity's value and/or verification state. CANNOT change 'primary' — use " +
        "users_identities_make_primary for that. Returns the updated identity. Write operation — honors " +
        "the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> IdentitiesUpdate(
        [Description("The numeric Zendesk user id.")]
        long userId,
        [Description("The numeric identity id to update.")]
        long identityId,
        [Description("The identity fields to change (value, verified); 'primary' is ignored here.")]
        ZendeskUserIdentityWrite identity,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"update identity {identityId} of user {userId}",
            () => zendeskApiClient.Users.UpdateIdentityAsync(userId, identityId, identity,
                cancellationToken: cancellationToken),
            new { userId, identityId, identity });

    /// <summary>Makes an identity the user's primary identity.</summary>
    [McpServerTool(Name = "users_identities_make_primary", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = true)]
    [Description(
        "Makes an identity the Zendesk user's PRIMARY identity (the way to change a user's primary e-mail — " +
        "users_update cannot do it). Returns the user's FULL identity list, reflecting the new primary. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes " +
        "made) in dry-run mode.")]
    public Task<object> IdentitiesMakePrimary(
        [Description("The numeric Zendesk user id.")]
        long userId,
        [Description("The numeric identity id to promote to primary.")]
        long identityId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"make identity {identityId} the primary identity of user {userId}",
            () => zendeskApiClient.Users.MakeIdentityPrimaryAsync(userId, identityId,
                cancellationToken: cancellationToken),
            new { userId, identityId });

    /// <summary>Marks a Zendesk user identity as verified.</summary>
    [McpServerTool(Name = "users_identities_verify", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = true)]
    [Description(
        "Marks a Zendesk user identity as verified without sending the user a verification e-mail (to send one " +
        "instead, use users_identities_request_verification). Returns the verified identity. Write " +
        "operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) " +
        "in dry-run mode.")]
    public Task<object> IdentitiesVerify(
        [Description("The numeric Zendesk user id.")]
        long userId,
        [Description("The numeric identity id to mark verified.")]
        long identityId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"mark identity {identityId} of user {userId} as verified",
            () => zendeskApiClient.Users.VerifyIdentityAsync(userId, identityId,
                cancellationToken: cancellationToken),
            new { userId, identityId });

    /// <summary>Sends a verification e-mail for a Zendesk user identity.</summary>
    [McpServerTool(Name = "users_identities_request_verification", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Sends the user a verification e-mail for an identity (each call sends another e-mail). To mark an " +
        "identity verified directly without e-mailing the user, use users_identities_verify. Returns a " +
        "completion acknowledgement. Write operation — honors the server execution mode: rejected in read-only " +
        "mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> IdentitiesRequestVerification(
        [Description("The numeric Zendesk user id.")]
        long userId,
        [Description("The numeric identity id to send the verification e-mail for.")]
        long identityId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"send a verification e-mail for identity {identityId} of user {userId}",
            () => zendeskApiClient.Users.RequestIdentityVerificationAsync(userId, identityId,
                cancellationToken: cancellationToken),
            new { userId, identityId });

    /// <summary>Deletes a Zendesk user identity.</summary>
    [McpServerTool(Name = "users_identities_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Deletes an identity (e-mail, phone number, social handle) from a Zendesk user. The primary identity " +
        "cannot be deleted — promote another identity with users_identities_make_primary first. Deleting a " +
        "messaging identity can break the messaging channel for the user. Returns a completion acknowledgement. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes " +
        "made) in dry-run mode.")]
    public Task<object> IdentitiesDelete(
        [Description("The numeric Zendesk user id.")]
        long userId,
        [Description("The numeric identity id to delete.")]
        long identityId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"delete identity {identityId} of user {userId}",
            () => zendeskApiClient.Users.DeleteIdentityAsync(userId, identityId,
                cancellationToken: cancellationToken),
            new { userId, identityId });
}
