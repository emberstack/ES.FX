using System.ComponentModel;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tools.Models;
using ES.FX.Zendesk.Support;
using ES.FX.Zendesk.Support.Models;
using Microsoft.Kiota.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP write tools for Zendesk users — create, update, merge, delete, and identity management. Namespaced
///     <c>users_*</c>. Every tool honors the server execution mode via
///     <see
///         cref="ZendeskToolInvoker.InvokeWriteAsync{T}(Execution.IMcpExecutionModeAccessor, string, Func{Task{T}}, object)" />
///     .
/// </summary>
/// <remarks>
///     Requests are built through the generated request builders (with bodies attached where the published spec
///     lost them), but sent as raw JSON (<see cref="ZendeskKiotaRequests.SendForJsonAsync" />) rather than
///     round-tripped through the generated models, whose read-only markings (user <c>id</c>/<c>created_at</c>/
///     <c>updated_at</c>, identity <c>id</c>/<c>verified</c>/<c>primary</c>, the whole <c>job_status</c>, ...)
///     would silently drop the server-assigned fields. Responses are then projected to <b>lean confirmations</b>
///     instead of echoing whole records: creates return the id plus a few identity fields and <c>created_at</c>;
///     updates return <c>{id, updated_at}</c> plus the server-state values of exactly the fields sent
///     (echo-of-change — a value differing from the request reveals a business-rule override without a follow-up
///     get); bulk jobs collapse to <c>{id, status}</c>; deletes acknowledge with a structured id instead of
///     returning the (personal) record. The upsert reads the HTTP status through
///     <see cref="ZendeskKiotaRequests.SendForJsonWithStatusAsync" /> to report <c>created: true|false</c>
///     (<c>201</c> created vs <c>200</c> updated).
/// </remarks>
[McpServerToolType]
public sealed class ZendeskUserWriteTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IMcpExecutionModeAccessor executionMode)
{
    /// <summary>
    ///     Serializer for the identity write bodies the published spec lost (the generated identity create/update
    ///     operations carry no request body) and for reading a write model's wire-named present-field set (the
    ///     echo-of-change in update confirmations): the curated models' snake_case <see cref="JsonPropertyName" />
    ///     mappings produce the documented wire shape, and unset (<c>null</c>) fields are omitted.
    /// </summary>
    private static readonly JsonSerializerOptions WriteJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Creates a Zendesk user.</summary>
    [McpServerTool(Name = "users_create", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a Zendesk user. 'email' becomes the primary e-mail identity; duplicate e-mail fails 422 — use " +
        "users_create_or_update to upsert. role: end-user|agent|admin (omit=end-user). skip_verify_email " +
        "suppresses the verification e-mail. Returns {id, name, email, role, created_at}; users_get for full " +
        "record. Write op honoring server execution mode: rejected read-only, simulated (no changes) dry-run.")]
    public Task<object> Create(
        [Description("User to create (name, email, role, phone, external_id, organization_id, tags, ...).")]
        ZendeskUserWrite user,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create user '{user.Name ?? user.Email ?? "(unnamed)"}'",
            async () =>
            {
                var request = zendesk.Api.V2.Users.ToPostRequestInformation(
                    new UserRequest { User = MapUser(user) });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return CreateConfirmation(UnwrapEntity(json, "user"));
            },
            user);

    /// <summary>Creates or updates a Zendesk user matched by e-mail or external id (upsert).</summary>
    [McpServerTool(Name = "users_create_or_update", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Upsert: create user, or update existing one matched by e-mail or external_id. Avoids the 422 " +
        "duplicate-email failure of users_create. external_id match is case-insensitive but stored external_id is " +
        "updated to the case you supply. Returns created:true {id, name, email, role, created_at} for a new user " +
        "(no role => end-user), else created:false {id, updated_at} plus server-state values of the fields you " +
        "sent. Write op honoring server execution mode: rejected read-only, simulated (no changes) dry-run.")]
    public Task<object> CreateOrUpdate(
        [Description("User to create or update — matched to existing user by email or external_id.")]
        ZendeskUserWrite user,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create or update user '{user.Name ?? user.Email ?? user.ExternalId ?? "(unspecified)"}'",
            async () =>
            {
                var request = zendesk.Api.V2.Users.Create_or_update.ToPostRequestInformation(
                    new UserRequest { User = MapUser(user) });
                // The 200-vs-201 distinction is the created:false|true signal — captured via the status-aware
                // send; the request on the wire is identical.
                var (statusCode, json) = await requestAdapter
                    .SendForJsonWithStatusAsync(request, cancellationToken).ConfigureAwait(false);
                var created = statusCode is HttpStatusCode.Created;
                var entity = UnwrapEntity(json, "user");
                var confirmation = new JsonObject { ["created"] = created };
                if (created)
                {
                    CopyFields(entity, confirmation, "id", "name", "email", "role", "created_at");
                }
                else
                {
                    CopyFields(entity, confirmation, "id", "updated_at");
                    AppendEchoOfChange(entity, confirmation, user);
                }

                return JsonSerializer.SerializeToElement(confirmation);
            },
            user);

    /// <summary>Creates up to 100 Zendesk users as an async job.</summary>
    [McpServerTool(Name = "users_create_many", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates up to 100 Zendesk users as an async job. Bulk user imports are off by default — Zendesk support " +
        "must enable them for the account or the call returns 403. Returns {id, status}; poll job_statuses_get by " +
        "id until completed — per-user outcomes (incl. partial failures) are in the job's results. Write " +
        "op honoring server execution mode: rejected read-only, simulated (no changes) dry-run.")]
    public Task<object> CreateMany(
        [Description("Users to create (1-100 per call).")]
        ZendeskUserWrite[] users,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create {users.Length} users as an async job",
            async () =>
            {
                ValidateBulkCount(users.Length, nameof(users));
                var request = zendesk.Api.V2.Users.Create_many.ToPostRequestInformation(
                    new UsersRequest { Users = users.Select(MapUser).ToList() });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return JobConfirmation(json);
            },
            () =>
            {
                // The dry run enforces the same contract the real call would.
                ValidateBulkCount(users.Length, nameof(users));
                return ZendeskDryRunResult.ForBulk($"create {users.Length} users as an async job",
                    "create", "users", users);
            });

    /// <summary>Creates or updates up to 100 Zendesk users as an async job.</summary>
    [McpServerTool(Name = "users_create_or_update_many", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Upsert up to 100 Zendesk users as an async job — each item matched to existing user by e-mail or " +
        "external_id. Same gating as users_create_many: bulk user imports must be enabled by Zendesk support or " +
        "the call returns 403. Returns {id, status}; poll job_statuses_get by id until completed. Write " +
        "op honoring server execution mode: rejected read-only, simulated (no changes) dry-run.")]
    public Task<object> CreateOrUpdateMany(
        [Description("Users to create or update (1-100 per call), matched by email or external_id.")]
        ZendeskUserWrite[] users,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create or update {users.Length} users as an async job",
            async () =>
            {
                ValidateBulkCount(users.Length, nameof(users));
                var request = zendesk.Api.V2.Users.Create_or_update_many.ToPostRequestInformation(
                    new UsersRequest { Users = users.Select(MapUser).ToList() });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return JobConfirmation(json);
            },
            () =>
            {
                ValidateBulkCount(users.Length, nameof(users));
                return ZendeskDryRunResult.ForBulk($"create or update {users.Length} users as an async job",
                    "create_or_update", "users", users);
            });

    // ── Single-action user writes (decomposed from the former composite users_update) ─────────────────────────
    // Each sets ONE aspect of a user so a consuming agent can be granted individual actions via its include-list
    // (e.g. profile edits without users_role_set / users_suspended_set — the privilege/access-sensitive ones).
    // All route through ApplyUserUpdate → the same PUT /users/{id} + execution-mode gate + echo-of-change.

    /// <summary>Sets a user's role (privilege level).</summary>
    [McpServerTool(Name = "users_role_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Set a user's ROLE — a privilege change: end-user|agent|admin (agent/admin grant Zendesk access). Returns " +
        "{id, updated_at, role} (server value reveals any override). Write op honoring server execution mode: " +
        "rejected read-only, simulated (no changes) dry-run.")]
    public Task<object> RoleSet(
        [Description("Numeric Zendesk user id.")]
        long id,
        [Description("end-user|agent|admin.")] string role,
        CancellationToken cancellationToken)
        => ApplyUserUpdate(id, new ZendeskUserWrite { Role = role }, $"set user {id} role to '{role}'",
            cancellationToken);

    /// <summary>Suspends or unsuspends a user.</summary>
    [McpServerTool(Name = "users_suspended_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Suspend (true) or unsuspend (false) a user — a suspended user cannot sign in or submit tickets. Returns " +
        "{id, updated_at, suspended}. Write op honoring server execution mode: rejected read-only, simulated (no " +
        "changes) dry-run.")]
    public Task<object> SuspendedSet(
        [Description("Numeric Zendesk user id.")]
        long id,
        [Description("true = suspend; false = unsuspend.")]
        bool suspended,
        CancellationToken cancellationToken)
        => ApplyUserUpdate(id, new ZendeskUserWrite { Suspended = suspended },
            $"{(suspended ? "suspend" : "unsuspend")} user {id}", cancellationToken);

    /// <summary>Sets which tickets a user may access.</summary>
    [McpServerTool(Name = "users_ticket_restriction_set", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Set which tickets a user may access (a permission change): organization|groups|assigned|requested. " +
        "Returns {id, updated_at, ticket_restriction}. Write op honoring server execution mode: rejected " +
        "read-only, simulated (no changes) dry-run.")]
    public Task<object> TicketRestrictionSet(
        [Description("Numeric Zendesk user id.")]
        long id,
        [Description("organization|groups|assigned|requested.")]
        string ticketRestriction,
        CancellationToken cancellationToken)
        => ApplyUserUpdate(id, new ZendeskUserWrite { TicketRestriction = ticketRestriction },
            $"set the ticket access restriction of user {id} to '{ticketRestriction}'", cancellationToken);

    /// <summary>Sets a user's name.</summary>
    [McpServerTool(Name = "users_name_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Set a user's display name. Returns {id, updated_at, name}. Write op honoring server execution mode: " +
        "rejected read-only, simulated (no changes) dry-run.")]
    public Task<object> NameSet(
        [Description("Numeric Zendesk user id.")]
        long id,
        [Description("New display name.")] string name,
        CancellationToken cancellationToken)
        => ApplyUserUpdate(id, new ZendeskUserWrite { Name = name }, $"set the name of user {id}", cancellationToken);

    /// <summary>Sets a user's phone number.</summary>
    [McpServerTool(Name = "users_phone_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Set a user's phone number. To manage e-mail/social identities use the users_identities_* tools. Returns " +
        "{id, updated_at, phone}. Write op honoring server execution mode: rejected read-only, simulated (no " +
        "changes) dry-run.")]
    public Task<object> PhoneSet(
        [Description("Numeric Zendesk user id.")]
        long id,
        [Description("New phone number.")] string phone,
        CancellationToken cancellationToken)
        => ApplyUserUpdate(id, new ZendeskUserWrite { Phone = phone }, $"set the phone number of user {id}",
            cancellationToken);

    /// <summary>Sets a user's default organization.</summary>
    [McpServerTool(Name = "users_organization_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Set a user's default organization by id — WARNING: this REMOVES the user's other organization " +
        "memberships. To add a membership without removing others use organizations_memberships_create. Returns " +
        "{id, updated_at, organization_id}. Write op honoring server execution mode: rejected read-only, simulated " +
        "(no changes) dry-run.")]
    public Task<object> OrganizationSet(
        [Description("Numeric Zendesk user id.")]
        long id,
        [Description("Numeric organization id.")]
        long organizationId,
        CancellationToken cancellationToken)
        => ApplyUserUpdate(id, new ZendeskUserWrite { OrganizationId = organizationId },
            $"set the default organization of user {id} to {organizationId}", cancellationToken);

    /// <summary>Sets a user's notes and/or details.</summary>
    [McpServerTool(Name = "users_notes_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Set a user's agent-only notes and/or details fields (free text; provide at least one). Returns {id, " +
        "updated_at} plus the fields you set. Write op honoring server execution mode: rejected read-only, " +
        "simulated (no changes) dry-run.")]
    public Task<object> NotesSet(
        [Description("Numeric Zendesk user id.")]
        long id,
        [Description("Agent-only notes (free text). Provide notes and/or details.")]
        string? notes = null,
        [Description("Agent-only details (free text). Provide notes and/or details.")]
        string? details = null,
        CancellationToken cancellationToken = default)
        => ApplyUserUpdate(id, new ZendeskUserWrite { Notes = notes, Details = details },
            $"set notes/details on user {id}", cancellationToken,
            () =>
            {
                if (notes is null && details is null)
                    throw new ArgumentException("Provide notes and/or details.", nameof(notes));
            });

    /// <summary>Replaces a user's whole tag set.</summary>
    [McpServerTool(Name = "users_tags_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "REPLACE a user's whole tag set with the given tags (pass an empty list to clear). Returns {id, " +
        "updated_at, tags}. Write op honoring server execution mode: rejected read-only, simulated (no changes) " +
        "dry-run.")]
    public Task<object> TagsSet(
        [Description("Numeric Zendesk user id.")]
        long id,
        [Description("Complete new tag set; existing tags not listed here are removed.")]
        string[] tags,
        CancellationToken cancellationToken)
        => ApplyUserUpdate(id, new ZendeskUserWrite { Tags = tags }, $"replace the tag set of user {id}",
            cancellationToken);

    /// <summary>Sets a user's custom field values.</summary>
    [McpServerTool(Name = "users_fields_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Set custom user-field values — a JSON object keyed by each field's KEY (not a numeric id): " +
        "{\"<field_key>\": <value>}. Value type follows the field type (dropdown/multiselect=option tag value, " +
        "checkbox=boolean, date=\"YYYY-MM-DD\", number). Only the listed fields change. Get field keys from your " +
        "Zendesk admin or from user_fields on an existing record (users_get). Returns {id, updated_at} plus the " +
        "fields you set. Write op honoring server execution mode: rejected read-only, simulated (no changes) " +
        "dry-run.")]
    public Task<object> FieldsSet(
        [Description("Numeric Zendesk user id.")]
        long id,
        [Description("Custom user-field values, keyed by field key: {\"<field_key>\": <value>}.")]
        Dictionary<string, object?> userFields,
        CancellationToken cancellationToken)
        => ApplyUserUpdate(id, new ZendeskUserWrite { UserFields = userFields },
            $"set {userFields.Count} custom field(s) on user {id}", cancellationToken,
            () =>
            {
                if (userFields.Count == 0)
                    throw new ArgumentException("Provide at least one custom field value.", nameof(userFields));
            });

    /// <summary>Merges one end user into another; the loser is absorbed and the winner survives.</summary>
    [McpServerTool(Name = "users_merge", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Merges one Zendesk end user INTO another: loserUserId is ABSORBED (their tickets and identities move to " +
        "the winner; loser ceases to exist as a separate user); winnerUserId survives. End users only — " +
        "agents/admins cannot be merged, nor can end users created by sharing agreements. Loser must be a " +
        "requester on 10,000 or fewer tickets or the merge is blocked. Cannot be undone. Returns {id, updated_at} " +
        "of the surviving winner; users_get for full record. Write op honoring server execution mode: rejected " +
        "read-only, simulated (no changes) dry-run.")]
    public Task<object> Merge(
        [Description("Id of the user to be absorbed (the LOSER — merged away).")]
        long loserUserId,
        [Description("Id of the user that survives the merge (the WINNER).")]
        long winnerUserId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"merge users: {loserUserId} (loser) into {winnerUserId} (winner)",
            // DIRECTION: the path user (loser) is absorbed INTO the body user (winner); the winner is returned.
            async () =>
            {
                var request = zendesk.Api.V2.Users[loserUserId].Merge.ToPutRequestInformation(
                    new UserRequest { User = new UserInput { Id = winnerUserId } });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                var winner = UnwrapEntity(json, "user");
                var confirmation = new JsonObject();
                CopyFields(winner, confirmation, "id", "updated_at");
                return JsonSerializer.SerializeToElement(confirmation);
            },
            new { loserUserId, winnerUserId });

    /// <summary>Soft-deletes a Zendesk user.</summary>
    [McpServerTool(Name = "users_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Soft-deletes a Zendesk user. Documented by Zendesk as NOT recoverable; a GDPR purge additionally " +
        "requires users_delete_permanently afterwards. Returns an acknowledgement carrying the user id — the " +
        "soft-deleted record is not echoed back. Write op honoring server execution mode: rejected read-only, " +
        "simulated (no changes) dry-run.")]
    public Task<object> Delete(
        [Description("Numeric Zendesk user id to delete.")]
        long id,
        CancellationToken cancellationToken)
    {
        var action = $"delete user {id}";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                // Zendesk answers 200 with the soft-deleted user (active=false) — not 204. The record is
                // deliberately NOT echoed back: the personal data adds nothing over the structured id.
                await requestAdapter.SendForJsonAsync(
                    zendesk.Api.V2.Users[id].ToDeleteRequestInformation(), cancellationToken).ConfigureAwait(false);
                return Acknowledge(action, id);
            },
            new { id });
    }

    /// <summary>Soft-deletes up to 100 Zendesk users as an async job.</summary>
    [McpServerTool(Name = "users_delete_many", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Soft-deletes up to 100 Zendesk users as an async job (admin-only). Documented by Zendesk as NOT " +
        "recoverable. Returns {id, status}; poll job_statuses_get by id until completed. Write op " +
        "honoring server execution mode: rejected read-only, simulated (no changes) dry-run.")]
    public Task<object> DeleteMany(
        [Description("Numeric Zendesk user ids to delete (1-100 per call).")]
        long[] ids,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"delete {ids.Length} users",
            async () =>
            {
                ValidateBulkCount(ids.Length, nameof(ids));
                var request =
                    zendesk.Api.V2.Users.Destroy_many.ToDeleteRequestInformation(cfg =>
                        cfg.QueryParameters.Ids = string.Join(',', ids));
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return JobConfirmation(json);
            },
            () =>
            {
                ValidateBulkCount(ids.Length, nameof(ids));
                return ZendeskDryRunResult.ForBulk($"delete {ids.Length} users", "delete", "users",
                    ids.Cast<object?>());
            });

    /// <summary>Permanently deletes an already soft-deleted Zendesk user. Irreversible.</summary>
    [McpServerTool(Name = "users_delete_permanently", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "PERMANENTLY deletes a Zendesk user ALREADY soft-deleted (via users_delete or users_delete_many) — does " +
        "not work on active users. IRREVERSIBLE; used for GDPR purges. Zendesk enforces a dedicated rate limit of " +
        "700 permanent deletions per 10 minutes. Returns an acknowledgement carrying the user id; the purged " +
        "user's personal data is deliberately not returned. Write op honoring server execution mode: rejected " +
        "read-only, simulated (no changes) dry-run.")]
    public Task<object> DeletePermanently(
        [Description("Numeric id of the ALREADY soft-deleted user to purge.")]
        long deletedUserId,
        CancellationToken cancellationToken)
    {
        var action = $"permanently delete already-deleted user {deletedUserId}";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                // A GDPR purge whose confirmation re-surfaces the purged user's personal data would defeat the
                // point — only the acknowledgement with the structured id is returned.
                await requestAdapter.SendForJsonAsync(
                        zendesk.Api.V2.Deleted_users[deletedUserId].ToDeleteRequestInformation(), cancellationToken)
                    .ConfigureAwait(false);
                return Acknowledge(action, deletedUserId);
            },
            new { deletedUserId });
    }

    /// <summary>Adds an identity (e-mail, phone, social handle) to a Zendesk user.</summary>
    [McpServerTool(Name = "users_identities_create", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Adds an identity (e-mail, phone number, social handle) to a Zendesk user. 'primary' is only writable at " +
        "creation time — to promote an existing identity use users_identities_make_primary. Returns {id, user_id, " +
        "type, value, created_at}; users_identities_list for the full row. Write op honoring server execution " +
        "mode: rejected read-only, simulated (no changes) dry-run.")]
    public Task<object> IdentitiesCreate(
        [Description("Numeric Zendesk user id.")]
        long userId,
        [Description(
            "Identity to add (type, value, verified, primary, skip_verify_email). type: email|phone_number|" +
            "twitter|facebook|google|agent_forwarding (also any_channel|foreign|sdk|messaging).")]
        ZendeskUserIdentityWrite identity,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"add an identity to user {userId}",
            async () =>
            {
                // The published spec lost the request body of the identity create operation — attach the
                // documented { "identity": { ... } } envelope manually
                // (https://developer.zendesk.com/api-reference/ticketing/users/user_identities/; ledger row in
                // src/ES.FX.Zendesk/OpenApi/README.md).
                var request = zendesk.Api.V2.Users[userId].Identities.ToPostRequestInformation();
                request.SetStreamContent(new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(
                    new { identity }, WriteJsonOptions)), "application/json");
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                var created = UnwrapEntity(json, "identity");
                var confirmation = new JsonObject();
                CopyFields(created, confirmation, "id", "user_id", "type", "value", "created_at");
                return JsonSerializer.SerializeToElement(confirmation);
            },
            new { userId, identity });

    /// <summary>Updates a Zendesk user identity's value or verification state.</summary>
    [McpServerTool(Name = "users_identities_update", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Updates a Zendesk user identity's value and/or verification state. CANNOT change 'primary' — use " +
        "users_identities_make_primary. Returns {id, updated_at} plus server-state values of the fields you sent. " +
        "Write op honoring server execution mode: rejected read-only, simulated (no changes) dry-run.")]
    public Task<object> IdentitiesUpdate(
        [Description("Numeric Zendesk user id.")]
        long userId,
        [Description("Numeric identity id to update.")]
        long identityId,
        [Description("Identity fields to change (value, verified); 'primary' is ignored here.")]
        ZendeskUserIdentityWrite identity,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"update identity {identityId} of user {userId}",
            async () =>
            {
                // The published spec lost the request body of the identity update operation — attach the
                // documented { "identity": { ... } } envelope manually
                // (https://developer.zendesk.com/api-reference/ticketing/users/user_identities/; ledger row in
                // src/ES.FX.Zendesk/OpenApi/README.md).
                var request = zendesk.Api.V2.Users[userId].Identities[identityId].ToPutRequestInformation();
                request.SetStreamContent(new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(
                    new { identity }, WriteJsonOptions)), "application/json");
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return UpdateConfirmation(UnwrapEntity(json, "identity"), identity);
            },
            new { userId, identityId, identity });

    /// <summary>Makes an identity the user's primary identity.</summary>
    [McpServerTool(Name = "users_identities_make_primary", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Makes an identity the Zendesk user's PRIMARY identity (the way to change a user's primary e-mail — a " +
        "profile-field update cannot). Returns ONLY the affected identity row (at minimum {id, user_id, primary:true}); " +
        "full list via users_identities_list. Write op honoring server execution mode: rejected read-only, " +
        "simulated (no changes) dry-run.")]
    public Task<object> IdentitiesMakePrimary(
        [Description("Numeric Zendesk user id.")]
        long userId,
        [Description("Numeric identity id to promote to primary.")]
        long identityId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"make identity {identityId} the primary identity of user {userId}",
            // Collection-level operation: the response is the user's FULL identity list — post-filtered here
            // to the one row the agent asked about.
            async () =>
            {
                var json = await requestAdapter.SendForJsonAsync(
                    zendesk.Api.V2.Users[userId].Identities[identityId].Make_primary.ToPutRequestInformation(),
                    cancellationToken).ConfigureAwait(false);
                return AffectedIdentity(json, userId, identityId);
            },
            new { userId, identityId });

    /// <summary>Marks a Zendesk user identity as verified.</summary>
    [McpServerTool(Name = "users_identities_verify", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Marks a Zendesk user identity as verified without sending the user a verification e-mail (to send one " +
        "instead, use users_identities_request_verification). Returns {id, updated_at, verified}. Write op " +
        "honoring server execution mode: rejected read-only, simulated (no changes) dry-run.")]
    public Task<object> IdentitiesVerify(
        [Description("Numeric Zendesk user id.")]
        long userId,
        [Description("Numeric identity id to mark verified.")]
        long identityId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"mark identity {identityId} of user {userId} as verified",
            async () =>
            {
                var json = await requestAdapter.SendForJsonAsync(
                    zendesk.Api.V2.Users[userId].Identities[identityId].Verify.ToPutRequestInformation(),
                    cancellationToken).ConfigureAwait(false);
                var verified = UnwrapEntity(json, "identity");
                var confirmation = new JsonObject();
                CopyFields(verified, confirmation, "id", "updated_at", "verified");
                return JsonSerializer.SerializeToElement(confirmation);
            },
            new { userId, identityId });

    /// <summary>Sends a verification e-mail for a Zendesk user identity.</summary>
    [McpServerTool(Name = "users_identities_request_verification", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Sends the user a verification e-mail for an identity (each call sends another e-mail). To mark an " +
        "identity verified directly without e-mailing the user, use users_identities_verify. Returns an " +
        "acknowledgement carrying the identity id. Write op honoring server execution mode: rejected read-only, " +
        "simulated (no changes) dry-run.")]
    public Task<object> IdentitiesRequestVerification(
        [Description("Numeric Zendesk user id.")]
        long userId,
        [Description("Numeric identity id to send the verification e-mail for.")]
        long identityId,
        CancellationToken cancellationToken)
    {
        var action = $"send a verification e-mail for identity {identityId} of user {userId}";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                await zendesk.Api.V2.Users[userId].Identities[identityId].Request_verification
                    .PutAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                return Acknowledge(action, identityId);
            },
            new { userId, identityId });
    }

    /// <summary>Deletes a Zendesk user identity.</summary>
    [McpServerTool(Name = "users_identities_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Deletes an identity (e-mail, phone number, social handle) from a Zendesk user. The primary identity " +
        "cannot be deleted — promote another identity with users_identities_make_primary first. Deleting a " +
        "messaging identity can break the messaging channel for the user. Returns an acknowledgement carrying the " +
        "identity id. Write op honoring server execution mode: rejected read-only, simulated (no changes) " +
        "dry-run.")]
    public Task<object> IdentitiesDelete(
        [Description("Numeric Zendesk user id.")]
        long userId,
        [Description("Numeric identity id to delete.")]
        long identityId,
        CancellationToken cancellationToken)
    {
        var action = $"delete identity {identityId} of user {userId}";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                await zendesk.Api.V2.Users[userId].Identities[identityId]
                    .DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                return Acknowledge(action, identityId);
            },
            new { userId, identityId });
    }

    /// <summary>Validates a bulk-operation item count (Zendesk accepts 1–100 items per bulk request).</summary>
    private static void ValidateBulkCount(int count, string paramName)
    {
        if (count is 0 or > 100)
            throw new ArgumentException("Zendesk bulk operations accept between 1 and 100 items.", paramName);
    }

    /// <summary>
    ///     The shared partial-update path for the single-action user setters: sends the narrow write model as a
    ///     <c>PUT /users/{id}</c> and returns the lean update confirmation with echo-of-change. The optional
    ///     <paramref name="validate" /> hook runs INSIDE the execution-mode gate (so read-only rejects first). The
    ///     dry-run echoes exactly the fields the setter set (nulls omitted).
    /// </summary>
    private Task<object> ApplyUserUpdate(long id, ZendeskUserWrite change, string action,
        CancellationToken cancellationToken, Action? validate = null)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                validate?.Invoke();
                var request = zendesk.Api.V2.Users[id].ToPutRequestInformation(
                    new UserUpdateRequest { User = MapUserUpdate(change) });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return UpdateConfirmation(UnwrapEntity(json, "user"), change);
            },
            () =>
            {
                validate?.Invoke();
                var echo = new JsonObject { ["id"] = id };
                foreach (var (name, value) in JsonSerializer.SerializeToNode(change, WriteJsonOptions)!.AsObject())
                    if (name != "id")
                        echo[name] = value?.DeepClone();
                return new ZendeskDryRunResult
                {
                    Description = $"Dry run — no changes were made. This call would {action}.",
                    Request = echo
                };
            });

    /// <summary>
    ///     Unwraps the singular entity envelope of a write response (<c>{"user": {...}}</c> /
    ///     <c>{"identity": {...}}</c> / <c>{"job_status": {...}}</c>) as a mutable node for confirmation building.
    /// </summary>
    private static JsonObject UnwrapEntity(JsonElement response, string propertyName)
    {
        if (response.ValueKind is JsonValueKind.Object && response.TryGetProperty(propertyName, out var entity) &&
            entity.ValueKind is JsonValueKind.Object)
            return (JsonObject)JsonNode.Parse(entity.GetRawText())!;
        throw new McpException($"The Zendesk response carried no '{propertyName}' object where one was expected " +
                               "— the write may still have been applied; verify with users_get.");
    }

    /// <summary>Copies the listed fields that are present and non-null, preserving the given order.</summary>
    private static void CopyFields(JsonObject source, JsonObject target, params string[] fields)
    {
        foreach (var field in fields)
            if (source[field] is { } value)
                target[field] = value.DeepClone();
    }

    /// <summary>The lean create confirmation: the id, the identity fields, and <c>created_at</c>.</summary>
    private static JsonElement CreateConfirmation(JsonObject user)
    {
        var confirmation = new JsonObject();
        CopyFields(user, confirmation, "id", "name", "email", "role", "created_at");
        return JsonSerializer.SerializeToElement(confirmation);
    }

    /// <summary>
    ///     The lean update confirmation: <c>{id, updated_at}</c> plus the echo-of-change (see
    ///     <see cref="AppendEchoOfChange" />).
    /// </summary>
    private static JsonElement UpdateConfirmation(JsonObject entity, object request)
    {
        var confirmation = new JsonObject();
        CopyFields(entity, confirmation, "id", "updated_at");
        AppendEchoOfChange(entity, confirmation, request);
        return JsonSerializer.SerializeToElement(confirmation);
    }

    /// <summary>
    ///     Appends the echo-of-change to an update confirmation: the SERVER-state values of exactly the fields
    ///     present in the request (wire names via the omit-null serializer). Reading the values back from the
    ///     response — rather than echoing the request — reveals trigger/business-rule overrides without a
    ///     follow-up get. <c>user_fields</c> are post-filtered to the requested keys so a tenant's full
    ///     custom-field set never rides along; a requested field absent from the response means null/empty.
    /// </summary>
    private static void AppendEchoOfChange(JsonObject entity, JsonObject confirmation, object request)
    {
        if (JsonSerializer.SerializeToNode(request, WriteJsonOptions) is not JsonObject requested) return;
        foreach (var (field, requestedValue) in requested)
        {
            if (field is "id" || confirmation.ContainsKey(field)) continue;
            if (field is "user_fields")
            {
                if (requestedValue is not JsonObject requestedFields ||
                    entity["user_fields"] is not JsonObject serverFields) continue;
                var echoed = new JsonObject();
                foreach (var (key, _) in requestedFields)
                    if (serverFields[key] is { } fieldValue)
                        echoed[key] = fieldValue.DeepClone();
                if (echoed.Count > 0) confirmation["user_fields"] = echoed;
                continue;
            }

            if (entity[field] is { } value) confirmation[field] = value.DeepClone();
        }
    }

    /// <summary>
    ///     Projects an async-job response to the lean <c>{id, status}</c> confirmation: the job id is all an
    ///     agent needs to poll <c>job_statuses_get</c> — progress and per-item results arrive there, not here.
    /// </summary>
    private static JsonElement JobConfirmation(JsonElement response)
    {
        var jobStatus = UnwrapEntity(response, "job_status");
        var confirmation = new JsonObject();
        if (jobStatus["id"] is { } id) confirmation["id"] = id.DeepClone();
        if (jobStatus["status"] is { } status) confirmation["status"] = status.DeepClone();
        return JsonSerializer.SerializeToElement(confirmation);
    }

    /// <summary>
    ///     Filters the make-primary response (the user's FULL identity list) down to the one affected row,
    ///     summary-projected. The endpoint returns a single offset page, so the promoted identity can fall off
    ///     it — in that case the confirmation is synthesized from request facts: the ids are known, and a
    ///     successful call means the identity IS primary now.
    /// </summary>
    private static JsonElement AffectedIdentity(JsonElement response, long userId, long identityId)
    {
        if (response.ValueKind is JsonValueKind.Object &&
            JsonNode.Parse(response.GetRawText()) is JsonObject source &&
            source["identities"] is JsonArray identities)
            foreach (var identity in identities)
                if (identity is JsonObject row && row["id"] is JsonValue idValue &&
                    idValue.TryGetValue(out long id) && id == identityId)
                    return JsonSerializer.SerializeToElement(ZendeskLean.SummarizeEntity("identities", row)!);

        return JsonSerializer.SerializeToElement(new JsonObject
        {
            ["id"] = identityId,
            ["user_id"] = userId,
            ["primary"] = true
        });
    }

    /// <summary>
    ///     Builds the lean write acknowledgement for operations whose response body is deliberately not echoed
    ///     back (the deletes) or absent (<c>204</c>): the structured id spares the agent parsing the description.
    /// </summary>
    private static ZendeskWriteAcknowledgement Acknowledge(string action, long id) => new()
    {
        Description = $"Zendesk accepted the request to {action}.",
        Id = id
    };

    /// <summary>
    ///     Maps the curated user write model onto the generated <see cref="UserInput" /> (used by create,
    ///     create-or-update, the bulk jobs, and the batch update). Kiota omits unassigned properties on the wire,
    ///     matching the omit-null serialization of the curated model.
    /// </summary>
    private static UserInput MapUser(ZendeskUserWrite user)
    {
        var input = new UserInput
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            Phone = user.Phone,
            ExternalId = user.ExternalId,
            OrganizationId = user.OrganizationId,
            Verified = user.Verified,
            Suspended = user.Suspended,
            Notes = user.Notes,
            Details = user.Details,
            Tags = user.Tags?.ToList(),
            TicketRestriction = user.TicketRestriction,
            SkipVerifyEmail = user.SkipVerifyEmail
        };
        if (user.UserFields is not null)
        {
            var fields = new UserInput_user_fields();
            foreach (var (key, value) in user.UserFields) fields.AdditionalData[key] = value!;
            input.UserFields = fields;
        }

        return input;
    }

    /// <summary>
    ///     Maps the curated user write model onto the generated <see cref="UserUpdateInput" /> (used by the
    ///     single update and the same-change bulk update). The generated update input has no <c>id</c> field, so a
    ///     set <see cref="ZendeskUserWrite.Id" /> is carried via <c>AdditionalData</c> to keep the wire shape of
    ///     the old omit-null serializer.
    /// </summary>
    private static UserUpdateInput MapUserUpdate(ZendeskUserWrite user)
    {
        var input = new UserUpdateInput
        {
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            Phone = user.Phone,
            ExternalId = user.ExternalId,
            OrganizationId = user.OrganizationId,
            Verified = user.Verified,
            Suspended = user.Suspended,
            Notes = user.Notes,
            Details = user.Details,
            Tags = user.Tags?.ToList(),
            TicketRestriction = user.TicketRestriction,
            SkipVerifyEmail = user.SkipVerifyEmail
        };
        if (user.Id is not null) input.AdditionalData["id"] = user.Id.Value;
        if (user.UserFields is not null)
        {
            var fields = new UserUpdateInput_user_fields();
            foreach (var (key, value) in user.UserFields) fields.AdditionalData[key] = value!;
            input.UserFields = fields;
        }

        return input;
    }
}