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
///     MCP write tools for Zendesk organizations and organization memberships. Namespaced
///     <c>organizations_*</c>. Every tool honors the server execution mode via
///     <see
///         cref="ZendeskToolInvoker.InvokeWriteAsync{T}(Execution.IMcpExecutionModeAccessor, string, Func{Task{T}}, object)" />
///     .
/// </summary>
/// <remarks>
///     Requests are built through the generated request builders (with bodies attached where the published spec
///     lost them), but responses are read as the raw wire JSON
///     (<see cref="ZendeskKiotaRequests.SendForJsonAsync" />) rather than round-tripped through the generated
///     models: the spec marks server-assigned fields (organization <c>created_at</c>/<c>url</c>, the whole
///     <c>job_status</c> object, membership <c>id</c>/<c>user_id</c>/<c>organization_id</c>, ...) as read-only,
///     so Kiota's serializer would silently drop them from the tool result. The wire JSON is then collapsed to
///     lean confirmations: creates return a small identity confirmation, updates <c>{id, updated_at}</c> plus an
///     echo-of-change (the server-state values of exactly the requested fields), bulk jobs
///     <c>{id, status}</c>, and deletes a structured acknowledgement — the complete records stay reachable
///     via <c>organizations_get</c>. Bulk (<c>*_many</c>) dry runs return the
///     <see cref="ZendeskDryRunResult.ForBulk" /> digest instead of echoing up to 100 write models verbatim.
/// </remarks>
[McpServerToolType]
public sealed class ZendeskOrganizationWriteTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IMcpExecutionModeAccessor executionMode)
{
    /// <summary>Creates a Zendesk organization.</summary>
    [McpServerTool(Name = "organizations_create", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Create organization; name unique across account. Returns {id, name, created_at}; full record via " +
        "organizations_get. Write op: rejected in read-only mode, simulated in dry-run.")]
    public Task<object> Create(
        [Description("Organization to create; 'name' required, unique.")]
        ZendeskOrganizationWrite organization,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create organization '{organization.Name}'",
            async () =>
            {
                var request = zendesk.Api.V2.Organizations.ToPostRequestInformation(
                    new CreateOrganizationRequest { Organization = Map(organization) });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return CreateConfirmation(json);
            },
            organization);

    /// <summary>Creates up to 100 Zendesk organizations as an async job.</summary>
    [McpServerTool(Name = "organizations_create_many", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Create up to 100 organizations in one call. Returns {id, status}; poll job_statuses_get by id " +
        "until completed, per-item outcomes in job results. Write op: rejected in read-only mode, simulated in " +
        "dry-run.")]
    public Task<object> CreateMany(
        [Description("Organizations to create (1-100); each 'name' unique.")]
        ZendeskOrganizationWrite[] organizations,
        CancellationToken cancellationToken)
    {
        var action = $"create {organizations.Length} organizations";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                ValidateBulkCount(organizations.Length, nameof(organizations));
                // Spec gap: the generated create_many builder lost the request body — attach the
                // { "organizations": [...] } envelope (OrganizationsResponse serializes exactly that shape;
                // https://developer.zendesk.com/api-reference/ticketing/organizations/organizations/, ledger
                // row in src/ES.FX.Zendesk/OpenApi/README.md).
                var request = zendesk.Api.V2.Organizations.Create_many.ToPostRequestInformation();
                request.SetContentFromParsable(requestAdapter, "application/json",
                    new OrganizationsResponse { Organizations = organizations.Select(Map).ToList() });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return JobConfirmation(json);
            },
            () =>
            {
                ValidateBulkCount(organizations.Length, nameof(organizations));
                return ZendeskDryRunResult.ForBulk(action, "create", "organizations", organizations);
            });
    }

    /// <summary>Creates or updates a Zendesk organization, matching by id or external id.</summary>
    [McpServerTool(Name = "organizations_create_or_update", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Create or update organization; matched by 'id' or 'external_id', NOT name (existing name without matching " +
        "key errors). Returns {created, id, name, external_id, created_at, updated_at}; created:true=new, " +
        "false=updated existing. Full record via organizations_get. Write op: rejected in read-only mode, " +
        "simulated in dry-run.")]
    public Task<object> CreateOrUpdate(
        [Description("Organization to create or update; set 'id' or 'external_id' to update existing.")]
        ZendeskOrganizationWrite organization,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create or update organization '{organization.Name}'",
            async () =>
            {
                // Spec gap: the generated create_or_update builder lost the request body (ledger row in
                // src/ES.FX.Zendesk/OpenApi/README.md).
                var request = zendesk.Api.V2.Organizations.Create_or_update.ToPostRequestInformation();
                request.SetContentFromParsable(requestAdapter, "application/json",
                    new CreateOrganizationRequest { Organization = Map(organization) });
                // The 201-vs-200 status is the created|updated signal — the body alone cannot tell them apart.
                var (statusCode, json) = await requestAdapter
                    .SendForJsonWithStatusAsync(request, cancellationToken).ConfigureAwait(false);
                return UpsertConfirmation(statusCode, json);
            },
            organization);

    /// <summary>Updates a Zendesk organization by id.</summary>
    [McpServerTool(Name = "organizations_update", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Update organization by id; only fields set in payload change, except 'domain_names' which OVERWRITES the " +
        "list (send the complete list). Returns {id, updated_at} plus server-state values of exactly the fields " +
        "sent; compare against request to spot trigger/business-rule overrides without a follow-up " +
        "organizations_get. Write op: rejected in read-only mode, simulated in dry-run.")]
    public Task<object> Update(
        [Description("Numeric organization id.")]
        long id,
        [Description("Fields to change; 'domain_names' overwrites — send complete list.")]
        ZendeskOrganizationWrite organization,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"update organization {id}",
            async () =>
            {
                // Spec gap: the generated PUT builder lost the request body (ledger row in
                // src/ES.FX.Zendesk/OpenApi/README.md).
                var request = zendesk.Api.V2.Organizations[id].ToPutRequestInformation();
                request.SetContentFromParsable(requestAdapter, "application/json",
                    new CreateOrganizationRequest { Organization = Map(organization) });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return UpdateConfirmation(json, organization);
            },
            new { id, organization });

    /// <summary>Applies the same change to up to 100 organizations as an async job.</summary>
    [McpServerTool(Name = "organizations_update_many", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Apply the SAME change to up to 100 organizations by id; for per-organization changes use " +
        "organizations_update_many_batch. Returns {id, status}; poll job_statuses_get by id until " +
        "completed. Write op: rejected in read-only mode, simulated in dry-run.")]
    public Task<object> UpdateMany(
        [Description("Numeric organization ids to update (1-100).")]
        long[] ids,
        [Description("Change applied to every listed organization.")]
        ZendeskOrganizationWrite change,
        CancellationToken cancellationToken)
    {
        var action = $"update {ids.Length} organizations with the same change";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                ValidateBulkCount(ids.Length, nameof(ids));
                // Spec gap: the generated update_many builder lost the request body (ledger row in
                // src/ES.FX.Zendesk/OpenApi/README.md).
                var request = zendesk.Api.V2.Organizations.Update_many.ToPutRequestInformation(cfg =>
                    cfg.QueryParameters.Ids = string.Join(',', ids));
                request.SetContentFromParsable(requestAdapter, "application/json",
                    new CreateOrganizationRequest { Organization = Map(change) });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return JobConfirmation(json);
            },
            () =>
            {
                ValidateBulkCount(ids.Length, nameof(ids));
                return ZendeskDryRunResult.ForBulk(action, "update", "organizations",
                    ids.Select(targetId => DigestTarget(targetId, change)));
            });
    }

    /// <summary>Applies per-organization changes to up to 100 organizations as an async job.</summary>
    [McpServerTool(Name = "organizations_update_many_batch", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Apply PER-ORGANIZATION changes to up to 100 organizations in one call; every item must carry its 'id'. " +
        "For the same change across many ids use organizations_update_many. Returns {id, status}; poll " +
        "job_statuses_get by id until completed. Write op: rejected in read-only mode, simulated in dry-run.")]
    public Task<object> UpdateManyBatch(
        [Description("Per-organization changes (1-100); every item must include 'id'.")]
        ZendeskOrganizationWrite[] organizations,
        CancellationToken cancellationToken)
    {
        var action = $"update {organizations.Length} organizations (batch)";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                ValidateBatchItems(organizations);
                // Spec gap: the generated update_many builder lost the request body — attach the
                // { "organizations": [...] } batch envelope (ledger row in src/ES.FX.Zendesk/OpenApi/README.md).
                var request = zendesk.Api.V2.Organizations.Update_many.ToPutRequestInformation();
                request.SetContentFromParsable(requestAdapter, "application/json",
                    new OrganizationsResponse { Organizations = organizations.Select(Map).ToList() });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return JobConfirmation(json);
            },
            () =>
            {
                ValidateBatchItems(organizations);
                return ZendeskDryRunResult.ForBulk(action, "update", "organizations", organizations);
            });
    }

    /// <summary>Deletes a Zendesk organization by id.</summary>
    [McpServerTool(Name = "organizations_delete", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Delete organization by id. PERMANENT — no soft-delete or restore; user and ticket associations to the " +
        "organization are removed unrecoverably. Returns completion acknowledgement carrying the deleted id. " +
        "Write op: rejected in read-only mode, simulated in dry-run.")]
    public Task<object> Delete(
        [Description("Numeric organization id.")]
        long id,
        CancellationToken cancellationToken)
    {
        var action = $"delete organization {id}";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                await zendesk.Api.V2.Organizations[id].DeleteAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return Acknowledge(action, id);
            },
            new { id });
    }

    /// <summary>Deletes up to 100 Zendesk organizations as an async job.</summary>
    [McpServerTool(Name = "organizations_delete_many", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Delete up to 100 organizations by id. PERMANENT — no soft-delete or restore. Returns {id, status}; " +
        "poll job_statuses_get by id until completed. Write op: rejected in read-only mode, simulated in " +
        "dry-run.")]
    public Task<object> DeleteMany(
        [Description("Numeric organization ids to delete (1-100).")]
        long[] ids,
        CancellationToken cancellationToken)
    {
        var action = $"delete {ids.Length} organizations";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                ValidateBulkCount(ids.Length, nameof(ids));
                var request = zendesk.Api.V2.Organizations.Destroy_many.ToDeleteRequestInformation(cfg =>
                    cfg.QueryParameters.Ids = string.Join(',', ids));
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return JobConfirmation(json);
            },
            () =>
            {
                ValidateBulkCount(ids.Length, nameof(ids));
                return ZendeskDryRunResult.ForBulk(action, "delete", "organizations", ids.Cast<object?>());
            });
    }

    /// <summary>Merges one Zendesk organization into another (irreversible).</summary>
    [McpServerTool(Name = "organizations_merge", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Merge organization INTO another: loser is DELETED, its users, tickets and domain names move to winner; " +
        "other organization fields NOT carried over. Irreversible; admin-only. Async but NOT a job_status — the " +
        "returned organization_merge carries an opaque string id; poll organizations_merges_get with it until " +
        "status is 'complete'. Write op: rejected in read-only mode, simulated in dry-run.")]
    public Task<object> Merge(
        [Description("Id of the organization to merge and delete (loser).")]
        long loserOrganizationId,
        [Description("Id of the organization that absorbs the loser (winner).")]
        long winnerOrganizationId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"merge organizations: {loserOrganizationId} (loser) into {winnerOrganizationId} (winner)",
            async () =>
            {
                var request = zendesk.Api.V2.Organizations[loserOrganizationId].Merge.ToPostRequestInformation(
                    new OrganizationMergeRequest
                    {
                        OrganizationMerge = new OrganizationMergeRequest_organization_merge
                        {
                            WinnerId = winnerOrganizationId
                        }
                    });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                // The organization_merge is already small — the full view just drops the API self-link and nulls.
                return ZendeskLean.ToFullView(json);
            },
            new { loserOrganizationId, winnerOrganizationId });

    /// <summary>Links a user to an organization.</summary>
    [McpServerTool(Name = "organizations_memberships_create", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Link a user to an organization by creating an organization membership. Fails 422 if membership already " +
        "exists. Returns created membership (null fields and API self-links omitted). Write op: rejected in " +
        "read-only mode, simulated in dry-run.")]
    public Task<object> MembershipsCreate(
        [Description("Numeric user id.")] long userId,
        [Description("Numeric organization id.")]
        long organizationId,
        [Description("Whether the new membership becomes the user's default organization (optional).")]
        bool? makeDefault = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"link user {userId} to organization {organizationId}",
            async () =>
            {
                // Spec gap: the generated POST builder lost the request body — attach the
                // { "organization_membership": {...} } envelope
                // (https://developer.zendesk.com/api-reference/ticketing/organizations/organization_memberships/#create-membership;
                // ledger row in src/ES.FX.Zendesk/OpenApi/README.md).
                var request = zendesk.Api.V2.Organization_memberships.ToPostRequestInformation();
                request.SetContentFromParsable(requestAdapter, "application/json",
                    new OrganizationMembershipResponse
                    {
                        OrganizationMembership = MapMembership(userId, organizationId, makeDefault)
                    });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return MembershipConfirmation(json);
            },
            new { userId, organizationId, makeDefault });

    /// <summary>Creates up to 100 organization memberships as an async job.</summary>
    [McpServerTool(Name = "organizations_memberships_create_many", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Create up to 100 organization memberships (user-to-organization links) in one call; each item needs " +
        "'user_id' and 'organization_id'. Returns {id, status}; poll job_statuses_get by id until " +
        "completed. Write op: rejected in read-only mode, simulated in dry-run.")]
    public Task<object> MembershipsCreateMany(
        [Description("Memberships to create (1-100); each item needs 'user_id' and 'organization_id'.")]
        ZendeskOrganizationMembership[] memberships,
        CancellationToken cancellationToken)
    {
        var action = $"create {memberships.Length} organization memberships";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                ValidateBulkCount(memberships.Length, nameof(memberships));
                // Spec gap: the generated create_many builder lost the request body — attach the
                // { "organization_memberships": [...] } envelope
                // (https://developer.zendesk.com/api-reference/ticketing/organizations/organization_memberships/#create-many-memberships;
                // ledger row in src/ES.FX.Zendesk/OpenApi/README.md). Items are projected to
                // user_id/organization_id/default so read-model defaults (e.g. Id = 0) never leak into the request.
                var request = zendesk.Api.V2.Organization_memberships.Create_many.ToPostRequestInformation();
                request.SetContentFromParsable(requestAdapter, "application/json",
                    new OrganizationMembershipsResponse
                    {
                        OrganizationMemberships = memberships
                            .Select(m => MapMembership(m.UserId, m.OrganizationId, m.Default))
                            .ToList()
                    });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return JobConfirmation(json);
            },
            () =>
            {
                ValidateBulkCount(memberships.Length, nameof(memberships));
                return MembershipsDigest(action, memberships);
            });
    }

    /// <summary>Removes an organization membership by its membership id.</summary>
    [McpServerTool(Name = "organizations_memberships_delete", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Remove an organization membership by its MEMBERSHIP id (not user or organization id — list them with " +
        "organizations_memberships_list). Side effect: Zendesk schedules a job un-assigning the user's working " +
        "tickets for that organization. Returns completion acknowledgement carrying the deleted membership id. " +
        "Write op: rejected in read-only mode, simulated in dry-run.")]
    public Task<object> MembershipsDelete(
        [Description("Numeric organization membership id.")]
        long membershipId,
        CancellationToken cancellationToken)
    {
        var action = $"delete organization membership {membershipId}";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                await zendesk.Api.V2.Organization_memberships[membershipId]
                    .DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                return Acknowledge(action, membershipId);
            },
            new { membershipId });
    }

    /// <summary>Removes up to 100 organization memberships as an async job.</summary>
    [McpServerTool(Name = "organizations_memberships_delete_many", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Remove up to 100 organization memberships by their MEMBERSHIP ids. Returns {id, status}; poll " +
        "job_statuses_get by id until completed. Write op: rejected in read-only mode, simulated in dry-run.")]
    public Task<object> MembershipsDeleteMany(
        [Description("Numeric organization membership ids to remove (1-100).")]
        long[] membershipIds,
        CancellationToken cancellationToken)
    {
        var action = $"delete {membershipIds.Length} organization memberships";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                ValidateBulkCount(membershipIds.Length, nameof(membershipIds));
                // The generated builder types `ids` as an exploded array (ids=1&ids=2); Zendesk documents a
                // comma-separated list
                // (https://developer.zendesk.com/api-reference/ticketing/organizations/organization_memberships/#bulk-delete-memberships,
                // curl example ?ids=1,2,3 — ledger row in src/ES.FX.Zendesk/OpenApi/README.md), so supply the
                // raw joined value the template already declares.
                var request = zendesk.Api.V2.Organization_memberships.Destroy_many.ToDeleteRequestInformation();
                request.QueryParameters.Add("ids", string.Join(',', membershipIds));
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return JobConfirmation(json);
            },
            () =>
            {
                ValidateBulkCount(membershipIds.Length, nameof(membershipIds));
                return ZendeskDryRunResult.ForBulk(action, "delete", "organization_memberships",
                    membershipIds.Cast<object?>());
            });
    }

    /// <summary>Makes an organization membership the user's default, returning only the affected membership.</summary>
    [McpServerTool(Name = "organizations_memberships_make_default", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Make an organization membership the user's default organization. Returns ONLY the affected membership " +
        "({id, user_id, organization_id, default:true}), not the user's whole membership list; when the row is " +
        "beyond the first page Zendesk returns, confirmation is synthesized from the request as {id, user_id, " +
        "default:true}. Write op: rejected in read-only mode, simulated in dry-run.")]
    public Task<object> MembershipsMakeDefault(
        [Description("Numeric user id owning the membership.")]
        long userId,
        [Description("Numeric organization membership id to make default.")]
        long membershipId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"make organization membership {membershipId} the default for user {userId}",
            async () =>
            {
                // The endpoint returns the PLURAL organization_memberships envelope (the user's full list) —
                // only the affected row is confirmation-relevant, so it is post-filtered out by id.
                var json = await requestAdapter.SendForJsonAsync(
                        zendesk.Api.V2.Users[userId].Organization_memberships[membershipId].Make_default
                            .ToPutRequestInformation(), cancellationToken)
                    .ConfigureAwait(false);
                return MakeDefaultConfirmation(json, userId, membershipId);
            },
            new { userId, membershipId });

    /// <summary>Validates a bulk-operation item count (Zendesk accepts 1–100 items per bulk request).</summary>
    private static void ValidateBulkCount(int count, string paramName)
    {
        if (count is 0 or > 100)
            throw new ArgumentException("Zendesk bulk operations accept between 1 and 100 items.", paramName);
    }

    /// <summary>Validates a batch-update payload: the bulk count plus the per-item <c>id</c> requirement.</summary>
    private static void ValidateBatchItems(ZendeskOrganizationWrite[] organizations)
    {
        ValidateBulkCount(organizations.Length, nameof(organizations));
        if (organizations.Any(o => o.Id is null))
            throw new ArgumentException("Every batch update item must carry Id.", nameof(organizations));
    }

    /// <summary>
    ///     Unwraps the <c>organization</c> member of a write response, or throws when Zendesk omitted it.
    /// </summary>
    private static JsonObject UnwrapOrganization(JsonElement response) =>
        response.ValueKind is JsonValueKind.Object &&
        response.TryGetProperty("organization", out var organization) &&
        organization.ValueKind is JsonValueKind.Object
            ? (JsonObject)JsonNode.Parse(organization.GetRawText())!
            : throw new McpException("The Zendesk API returned no organization where one was expected — " +
                                     "the write may still have been applied; verify with organizations_get.");

    /// <summary>Copies the named fields that are present and non-null, preserving the given order.</summary>
    private static void Copy(JsonObject source, JsonObject target, params string[] fields)
    {
        foreach (var field in fields)
            if (source[field] is { } value)
                target[field] = value.DeepClone();
    }

    /// <summary>
    ///     The lean create confirmation — <c>{id, name, created_at}</c>. The complete record stays reachable via
    ///     <c>organizations_get</c>.
    /// </summary>
    private static JsonElement CreateConfirmation(JsonElement response)
    {
        var organization = UnwrapOrganization(response);
        var confirmation = new JsonObject();
        Copy(organization, confirmation, "id", "name", "created_at");
        return JsonSerializer.SerializeToElement(confirmation);
    }

    /// <summary>
    ///     The upsert confirmation: the create confirmation plus the matching keys, the update timestamp, and the
    ///     <c>created</c> flag derived from the HTTP status (<c>201</c> created, <c>200</c> updated).
    /// </summary>
    private static JsonElement UpsertConfirmation(HttpStatusCode statusCode, JsonElement response)
    {
        var organization = UnwrapOrganization(response);
        var confirmation = new JsonObject { ["created"] = statusCode is HttpStatusCode.Created };
        Copy(organization, confirmation, "id", "name", "external_id", "created_at", "updated_at");
        return JsonSerializer.SerializeToElement(confirmation);
    }

    /// <summary>
    ///     The update confirmation — <c>{id, updated_at}</c> plus the echo-of-change: the server-state values of
    ///     exactly the fields present in the request (custom fields filtered to the requested keys), so a
    ///     trigger/business-rule override is visible without a follow-up <c>organizations_get</c>.
    /// </summary>
    private static JsonElement UpdateConfirmation(JsonElement response, ZendeskOrganizationWrite change)
    {
        var organization = UnwrapOrganization(response);
        var confirmation = new JsonObject();
        Copy(organization, confirmation, "id", "updated_at");
        Copy(organization, confirmation, ChangedFields(change));
        if (change.OrganizationFields is { Count: > 0 } requestedFields &&
            organization["organization_fields"] is JsonObject serverFields)
        {
            var echoed = new JsonObject();
            foreach (var key in requestedFields.Keys)
                if (serverFields[key] is { } value)
                    echoed[key] = value.DeepClone();
            if (echoed.Count > 0) confirmation["organization_fields"] = echoed;
        }

        return JsonSerializer.SerializeToElement(confirmation);
    }

    /// <summary>The wire names of the top-level fields the write model actually sets.</summary>
    private static string[] ChangedFields(ZendeskOrganizationWrite change)
    {
        var fields = new List<string>();
        if (change.Name is not null) fields.Add("name");
        if (change.ExternalId is not null) fields.Add("external_id");
        if (change.DomainNames is not null) fields.Add("domain_names");
        if (change.Details is not null) fields.Add("details");
        if (change.Notes is not null) fields.Add("notes");
        if (change.Tags is not null) fields.Add("tags");
        if (change.SharedTickets is not null) fields.Add("shared_tickets");
        if (change.SharedComments is not null) fields.Add("shared_comments");
        return [.. fields];
    }

    /// <summary>
    ///     The lean bulk-job confirmation — <c>{id, status}</c>; the per-item outcomes stay reachable via
    ///     <c>job_statuses_get</c>. Throws when Zendesk returned no <c>job_status</c>.
    /// </summary>
    private static JsonElement JobConfirmation(JsonElement response)
    {
        if (response.ValueKind is not JsonValueKind.Object ||
            !response.TryGetProperty("job_status", out var jobStatus) ||
            jobStatus.ValueKind is not JsonValueKind.Object)
            throw new McpException("The Zendesk API returned no job status where one was expected — " +
                                   "the write may still have been applied; verify with organizations_get.");

        var source = (JsonObject)JsonNode.Parse(jobStatus.GetRawText())!;
        var confirmation = new JsonObject();
        if (source["id"] is { } id) confirmation["id"] = id.DeepClone();
        if (source["status"] is { } status) confirmation["status"] = status.DeepClone();
        return JsonSerializer.SerializeToElement(confirmation);
    }

    /// <summary>
    ///     The membership create confirmation: the unwrapped membership as full view (a membership is already
    ///     lean — the transform only drops the API self-link and null fields).
    /// </summary>
    private static JsonElement MembershipConfirmation(JsonElement response) =>
        response.ValueKind is JsonValueKind.Object &&
        response.TryGetProperty("organization_membership", out var membership) &&
        membership.ValueKind is JsonValueKind.Object
            ? ZendeskLean.ToFullView(membership)
            : throw new McpException("The Zendesk API returned no organization membership where one was expected — " +
                                     "the write may still have been applied; verify with organizations_get.");

    /// <summary>
    ///     Confirms a make-default write with ONLY the affected membership row, post-filtered by id from the
    ///     plural envelope the endpoint returns. The list is paged, so for a user with many memberships the row
    ///     can be off-page — the write still succeeded, so the confirmation is synthesized from request facts
    ///     (<c>{id, user_id, default: true}</c>) instead of paging after a write.
    /// </summary>
    private static JsonElement MakeDefaultConfirmation(JsonElement response, long userId, long membershipId)
    {
        if (response.ValueKind is JsonValueKind.Object &&
            response.TryGetProperty("organization_memberships", out var memberships) &&
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

    /// <summary>Builds the delete acknowledgement, carrying the affected id as a structured field.</summary>
    private static ZendeskWriteAcknowledgement Acknowledge(string action, long id) =>
        new() { Description = $"Zendesk accepted the request to {action}.", Id = id };

    /// <summary>
    ///     One same-change bulk dry-run digest item: the target id merged onto the shared change, so
    ///     <see cref="ZendeskDryRunResult.ForBulk" /> reports <c>{index, id, fields:[changed field names]}</c>
    ///     per organization.
    /// </summary>
    private static JsonObject DigestTarget(long id, ZendeskOrganizationWrite change)
    {
        var item = (JsonObject)JsonSerializer.SerializeToNode(change)!;
        item["id"] = id;
        return item;
    }

    /// <summary>
    ///     The bulk-membership dry-run digest. <see cref="ZendeskDryRunResult.ForBulk" /> lifts only
    ///     <c>id</c>/<c>external_id</c>/<c>subject</c> as per-item identity, but a membership is identified by
    ///     its <c>user_id</c>/<c>organization_id</c> pair — so the rows are built here in the same
    ///     <c>{action, target, count, items}</c> digest shape (the values are numeric ids; nothing to truncate).
    /// </summary>
    private static ZendeskDryRunResult MembershipsDigest(string action,
        ZendeskOrganizationMembership[] memberships)
    {
        var items = new JsonArray();
        for (var index = 0; index < memberships.Length; index++)
        {
            var membership = memberships[index];
            var row = new JsonObject { ["index"] = index };
            if (membership.UserId is { } userId) row["user_id"] = userId;
            if (membership.OrganizationId is { } organizationId) row["organization_id"] = organizationId;
            if (membership.Default is { } isDefault) row["default"] = isDefault;
            items.Add(row);
        }

        return new ZendeskDryRunResult
        {
            Description = $"Dry run — no changes were made. This call would {action}.",
            Request = new JsonObject
            {
                ["action"] = "create",
                ["target"] = "organization_memberships",
                ["count"] = memberships.Length,
                ["items"] = items
            }
        };
    }

    /// <summary>Maps the curated organization write model onto the generated request model.</summary>
    private static OrganizationObject Map(ZendeskOrganizationWrite organization)
    {
        var mapped = new OrganizationObject
        {
            Id = organization.Id,
            Name = organization.Name,
            ExternalId = organization.ExternalId,
            DomainNames = organization.DomainNames?.ToList(),
            Details = organization.Details,
            Notes = organization.Notes,
            Tags = organization.Tags?.ToList(),
            SharedTickets = organization.SharedTickets,
            SharedComments = organization.SharedComments
        };
        if (organization.OrganizationFields is { } fields)
        {
            var organizationFields = new OrganizationObject_organization_fields();
            foreach (var (key, value) in fields) organizationFields.AdditionalData[key] = value!;
            mapped.OrganizationFields = organizationFields;
        }

        return mapped;
    }

    /// <summary>
    ///     Builds a membership request item. The generated model marks <c>user_id</c>/<c>organization_id</c> as
    ///     read-only (the spec treats them as server-assigned), so they travel via <c>AdditionalData</c>, which
    ///     Kiota serializes as regular top-level fields. The docs establish both as mandatory writable create
    ///     fields
    ///     (https://developer.zendesk.com/api-reference/ticketing/organizations/organization_memberships/#create-membership).
    /// </summary>
    private static OrganizationMembershipObject MapMembership(long? userId, long? organizationId, bool? @default)
    {
        var membership = new OrganizationMembershipObject { Default = @default };
        if (userId is { } user) membership.AdditionalData["user_id"] = user;
        if (organizationId is { } organization) membership.AdditionalData["organization_id"] = organization;
        return membership;
    }
}

/// <summary>An organization membership — links a user to an organization.</summary>
public sealed record ZendeskOrganizationMembership
{
    /// <summary>The membership id (server-assigned; ignored on create).</summary>
    [JsonPropertyName("id")]
    public long Id { get; init; }

    /// <summary>The numeric Zendesk user id.</summary>
    [JsonPropertyName("user_id")]
    public long? UserId { get; init; }

    /// <summary>The numeric Zendesk organization id.</summary>
    [JsonPropertyName("organization_id")]
    public long? OrganizationId { get; init; }

    /// <summary>Whether this is the user's default organization.</summary>
    [JsonPropertyName("default")]
    public bool? Default { get; init; }

    /// <summary>When the membership was created (server-assigned; ignored on create).</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>When the membership was last updated (server-assigned; ignored on create).</summary>
    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; init; }
}