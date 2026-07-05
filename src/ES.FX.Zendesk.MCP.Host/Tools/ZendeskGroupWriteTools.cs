using System.ComponentModel;
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
///     MCP write tools for Zendesk groups and group memberships. Namespaced <c>groups_*</c>. Every tool
///     honors the server execution mode via
///     <see
///         cref="ZendeskToolInvoker.InvokeWriteAsync{T}(Execution.IMcpExecutionModeAccessor, string, Func{Task{T}}, object)" />
///     .
/// </summary>
/// <remarks>
///     Request bodies are mapped onto the generated models (so the wire shapes stay typed and validated), but
///     the requests are sent through <see cref="ZendeskKiotaRequests.SendForJsonAsync" /> (raw JSON passthrough)
///     wherever the response payload matters: the published spec marks the server-assigned fields agents need
///     (<c>id</c>, <c>created_at</c>, <c>updated_at</c>, the <c>job_status</c> state) as read-only, so Kiota's
///     serializer would silently drop them from the tool result. Successful writes return
///     <b>
///         lean
///         confirmations
///     </b>
///     instead of echoing the full Zendesk payload: creates return the new record's identity,
///     updates echo the server state of exactly the requested fields, deletes acknowledge with the affected id,
///     and bulk operations return <c>{id, status}</c> for <c>job_statuses_get</c> polling.
/// </remarks>
[McpServerToolType]
public sealed class ZendeskGroupWriteTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IMcpExecutionModeAccessor executionMode)
{
    /// <summary>Creates a Zendesk group.</summary>
    [McpServerTool(Name = "groups_create", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Creates a Zendesk group (agent team). is_public is set at creation and can never change to public " +
        "later. Returns {id, name, is_public, created_at}; full record via groups_get. " +
        "Write; read-only mode rejects, dry-run simulates.")]
    public Task<object> Create(
        [Description(
            "Group to create. 'name' required. is_public=false for a private group (irreversible).")]
        ZendeskGroupWrite group,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create group '{group.Name}'",
            async () =>
            {
                var request = zendesk.Api.V2.Groups.ToPostRequestInformation(
                    new CreateGroupRequest
                    {
                        Group = new CreateGroupRequest_group
                        {
                            Name = group.Name,
                            Description = group.Description,
                            IsPublic = group.IsPublic
                        }
                    });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildGroupCreateConfirmation(json);
            },
            group);

    /// <summary>Updates a Zendesk group by id.</summary>
    [McpServerTool(Name = "groups_update", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Updates a Zendesk group by id. Only payload fields change. A private group cannot be made public. " +
        "Returns {id, updated_at} plus server-state values of exactly the fields you sent — a value differing " +
        "from the request means a business rule adjusted it. Write; read-only mode rejects, dry-run simulates.")]
    public Task<object> Update(
        [Description("Numeric Zendesk group id.")]
        long id,
        [Description("Fields to change.")] ZendeskGroupWrite group,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"update group {id}",
            async () =>
            {
                var request = zendesk.Api.V2.Groups[id].ToPutRequestInformation(
                    new UpdateGroupRequest
                    {
                        Group = new UpdateGroupRequest_group
                        {
                            Name = group.Name,
                            Description = group.Description,
                            IsPublic = group.IsPublic
                        }
                    });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildGroupUpdateConfirmation(json, group);
            },
            new { id, group });

    /// <summary>Soft-deletes a Zendesk group by id.</summary>
    [McpServerTool(Name = "groups_delete", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Soft-deletes a Zendesk group by id. Returns an acknowledgement carrying the affected id. " +
        "Write; read-only mode rejects, dry-run simulates.")]
    public Task<object> Delete(
        [Description("Numeric Zendesk group id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"delete group {id}",
            async () =>
            {
                await zendesk.Api.V2.Groups[id].DeleteAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return new ZendeskWriteAcknowledgement
                {
                    Description = $"Zendesk accepted the request to delete group {id}.",
                    Id = id
                };
            },
            new { id });

    /// <summary>Assigns an agent to a group.</summary>
    [McpServerTool(Name = "groups_memberships_create", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Assigns a Zendesk agent to a group by creating a group membership. Returns {id, user_id, group_id, " +
        "default, created_at}. Write; read-only mode rejects, dry-run simulates.")]
    public Task<object> MembershipsCreate(
        [Description("Numeric Zendesk user id of the agent (must be an agent, not an end user).")]
        long userId,
        [Description("Numeric Zendesk group id.")]
        long groupId,
        [Description(
            "Optional. true makes this the agent's default group — tickets assigned directly to the agent " +
            "assume it.")]
        bool? makeDefault = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"assign user {userId} to group {groupId}",
            async () =>
            {
                var request = zendesk.Api.V2.Group_memberships.ToPostRequestInformation(
                    new CreateGroupMembershipRequest
                    {
                        GroupMembership = new CreateGroupMembershipRequest_group_membership
                        {
                            UserId = userId,
                            GroupId = groupId,
                            Default = makeDefault
                        }
                    });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildMembershipConfirmation(json);
            },
            new { userId, groupId, makeDefault });

    /// <summary>Assigns up to 100 agents to groups as an async job.</summary>
    [McpServerTool(Name = "groups_memberships_create_many", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Assigns up to 100 Zendesk agents to groups in one call. Returns {id, status} — poll " +
        "job_statuses_get by id until complete. Write; read-only mode rejects, dry-run simulates.")]
    public Task<object> MembershipsCreateMany(
        [Description(
            "Memberships to create (1-100). Each item needs an agent 'user_id' and a 'group_id'.")]
        ZendeskGroupMembership[] memberships,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create {memberships.Length} group memberships",
            async () =>
            {
                ValidateBulkCount(memberships.Length, nameof(memberships));
                // Project to a clean payload so read-model defaults (e.g. Id = 0) never leak into the request.
                var body = new BulkCreateGroupMembershipsRequest
                {
                    GroupMemberships = memberships
                        .Select(m => new BulkCreateGroupMembershipsRequest_group_memberships
                        {
                            UserId = m.UserId,
                            GroupId = m.GroupId,
                            Default = m.Default
                        })
                        .ToList()
                };
                var request = zendesk.Api.V2.Group_memberships.Create_many.ToPostRequestInformation(body);
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildJobConfirmation(json);
            },
            // The dry-run digest reports the PROJECTED wire payload, so read-model defaults never leak there
            // either — see ZendeskDryRunResult.ForBulk.
            () =>
            {
                ValidateBulkCount(memberships.Length, nameof(memberships));
                return ZendeskDryRunResult.ForBulk($"create {memberships.Length} group memberships", "create",
                    "group_memberships",
                    memberships.Select(m => new { user_id = m.UserId, group_id = m.GroupId, @default = m.Default }));
            });

    /// <summary>Removes a group membership by its membership id.</summary>
    [McpServerTool(Name = "groups_memberships_delete", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Removes a Zendesk group membership by its MEMBERSHIP id (not user or group id — list via " +
        "groups_memberships_list). Side effect: Zendesk schedules a job un-assigning the agent's working " +
        "tickets in that group. Returns an acknowledgement carrying the affected membership id. " +
        "Write; read-only mode rejects, dry-run simulates.")]
    public Task<object> MembershipsDelete(
        [Description(
            "Numeric group membership id (not a user or group id — get it from groups_memberships_list).")]
        long membershipId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"delete group membership {membershipId}",
            async () =>
            {
                await zendesk.Api.V2.Group_memberships[membershipId]
                    .DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                return new ZendeskWriteAcknowledgement
                {
                    Description = $"Zendesk accepted the request to delete group membership {membershipId}.",
                    Id = membershipId
                };
            },
            new { membershipId });

    /// <summary>Removes up to 100 group memberships as an async job.</summary>
    [McpServerTool(Name = "groups_memberships_delete_many", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Removes up to 100 Zendesk group memberships by their MEMBERSHIP ids. Side effect: Zendesk schedules a " +
        "job un-assigning the agents' working tickets in those groups. Returns {id, status} — poll " +
        "job_statuses_get by id until complete. Write; read-only mode rejects, dry-run simulates.")]
    public Task<object> MembershipsDeleteMany(
        [Description(
            "Numeric group membership ids to remove (1-100; not user or group ids — get them from " +
            "groups_memberships_list).")]
        long[] membershipIds,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"delete {membershipIds.Length} group memberships",
            async () =>
            {
                ValidateBulkCount(membershipIds.Length, nameof(membershipIds));
                var request = zendesk.Api.V2.Group_memberships.Destroy_many.ToDeleteRequestInformation(cfg =>
                    cfg.QueryParameters.Ids = string.Join(',', membershipIds));
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildJobConfirmation(json);
            },
            () =>
            {
                ValidateBulkCount(membershipIds.Length, nameof(membershipIds));
                return ZendeskDryRunResult.ForBulk($"delete {membershipIds.Length} group memberships", "delete",
                    "group_memberships", membershipIds.Cast<object>());
            });

    /// <summary>Makes a group membership the agent's default.</summary>
    [McpServerTool(Name = "groups_memberships_make_default", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Makes a group membership the agent's default group. Returns ONLY the affected membership (default=" +
        "true); if it isn't on the page Zendesk echoes back, a minimal {id, user_id, default} confirmation is " +
        "synthesized. Write; read-only mode rejects, dry-run simulates.")]
    public Task<object> MembershipsMakeDefault(
        [Description("Numeric Zendesk user id owning the membership.")]
        long userId,
        [Description(
            "Numeric group membership id to make default (not a user or group id; must belong to the given " +
            "userId).")]
        long membershipId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"make group membership {membershipId} the default for user {userId}",
            async () =>
            {
                var request = zendesk.Api.V2.Users[userId].Group_memberships[membershipId].Make_default
                    .ToPutRequestInformation();
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildMakeDefaultConfirmation(json, userId, membershipId);
            },
            new { userId, membershipId });

    /// <summary>Replicates the retired client's bulk-size validation (same message and semantics).</summary>
    private static void ValidateBulkCount(int count, string paramName)
    {
        if (count is 0 or > 100)
            throw new ArgumentException("Zendesk bulk operations accept between 1 and 100 items.", paramName);
    }

    /// <summary>The create confirmation: the new group's identity only — <c>groups_get</c> is the full sink.</summary>
    private static JsonElement BuildGroupCreateConfirmation(JsonElement response)
    {
        var group = UnwrapObject(response, "group");
        var confirmation = new JsonObject();
        Copy(group, confirmation, "id", "name", "is_public", "created_at");
        return JsonSerializer.SerializeToElement(confirmation);
    }

    /// <summary>
    ///     The update confirmation: <c>{id, updated_at}</c> plus the <b>server-state</b> values of exactly the
    ///     fields present in the request (echo-of-change) — a value differing from the request reveals a
    ///     business-rule override without a follow-up <c>groups_get</c>.
    /// </summary>
    private static JsonElement BuildGroupUpdateConfirmation(JsonElement response, ZendeskGroupWrite requested)
    {
        var group = UnwrapObject(response, "group");
        var confirmation = new JsonObject();
        Copy(group, confirmation, "id", "updated_at");
        if (requested.Name is not null) Copy(group, confirmation, "name");
        if (requested.Description is not null) Copy(group, confirmation, "description");
        if (requested.IsPublic is not null) Copy(group, confirmation, "is_public");
        return JsonSerializer.SerializeToElement(confirmation);
    }

    /// <summary>The membership-create confirmation: the created membership's routing identity only.</summary>
    private static JsonElement BuildMembershipConfirmation(JsonElement response)
    {
        var membership = UnwrapObject(response, "group_membership");
        var confirmation = new JsonObject();
        Copy(membership, confirmation, "id", "user_id", "group_id", "default", "created_at");
        return JsonSerializer.SerializeToElement(confirmation);
    }

    /// <summary>
    ///     The bulk confirmation: <c>{id, status}</c> — the two values <c>job_statuses_get</c> polling needs.
    /// </summary>
    private static JsonElement BuildJobConfirmation(JsonElement response)
    {
        var job = UnwrapObject(response, "job_status");
        var confirmation = new JsonObject();
        if (job["id"] is { } id) confirmation["id"] = id.DeepClone();
        if (job["status"] is { } status) confirmation["status"] = status.DeepClone();
        return JsonSerializer.SerializeToElement(confirmation);
    }

    /// <summary>
    ///     The make-default confirmation: ONLY the affected membership row, post-filtered by id from the full
    ///     membership list the endpoint echoes. When the row is not on the returned page (the endpoint returns
    ///     only the first page for agents with many memberships), the confirmation is synthesized from request
    ///     facts instead — the write succeeded (a failure would have thrown), so <c>{id, user_id, default:true}</c>
    ///     is known to hold.
    /// </summary>
    private static JsonElement BuildMakeDefaultConfirmation(JsonElement response, long userId, long membershipId)
    {
        if (response.ValueKind is JsonValueKind.Object &&
            response.TryGetProperty("group_memberships", out var memberships) &&
            memberships.ValueKind is JsonValueKind.Array)
            foreach (var membership in memberships.EnumerateArray())
                if (membership.ValueKind is JsonValueKind.Object &&
                    membership.TryGetProperty("id", out var id) &&
                    id.ValueKind is JsonValueKind.Number && id.GetInt64() == membershipId)
                    return ZendeskLean.ToFullView(membership);

        return JsonSerializer.SerializeToElement(new JsonObject
        {
            ["id"] = membershipId,
            ["user_id"] = userId,
            ["default"] = true
        });
    }

    /// <summary>Unwraps a write response's payload member, failing loudly when Zendesk returned something else.</summary>
    private static JsonObject UnwrapObject(JsonElement response, string propertyName)
    {
        if (response.ValueKind is JsonValueKind.Object && response.TryGetProperty(propertyName, out var payload) &&
            payload.ValueKind is JsonValueKind.Object)
            return (JsonObject)JsonNode.Parse(payload.GetRawText())!;
        throw new McpException(
            $"The Zendesk API response carried no '{propertyName}' payload where one was expected — " +
            "the write may still have been applied; verify with groups_get.");
    }

    /// <summary>Copies the allowlisted fields that are present and non-null, preserving the given order.</summary>
    private static void Copy(JsonObject source, JsonObject target, params string[] fields)
    {
        foreach (var field in fields)
            if (source[field] is { } value)
                target[field] = value.DeepClone();
    }
}

/// <summary>
///     A membership linking an agent (<see cref="UserId" />) to a group (<see cref="GroupId" />). Input shape for
///     <see cref="ZendeskGroupWriteTools.MembershipsCreateMany" /> — only <c>user_id</c>, <c>group_id</c> and
///     <c>default</c> are sent to Zendesk.
/// </summary>
public sealed record ZendeskGroupMembership
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("user_id")] public long? UserId { get; init; }
    [JsonPropertyName("group_id")] public long? GroupId { get; init; }

    /// <summary>Whether this is the user's default group.</summary>
    [JsonPropertyName("default")]
    public bool? Default { get; init; }

    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
}