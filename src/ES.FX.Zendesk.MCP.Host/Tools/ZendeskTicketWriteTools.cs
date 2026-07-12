using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tools.Models;
using ES.FX.Zendesk.Support;
using ES.FX.Zendesk.Support.Api.V2.Tickets.Item.Tags;
using ES.FX.Zendesk.Support.Models;
using Microsoft.Kiota.Abstractions;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP write tools for Zendesk tickets (create/update/delete/merge/spam/restore/tags/comments/import).
///     Namespaced <c>tickets_*</c>; every tool routes through <see cref="ZendeskToolInvoker" /> so the
///     server execution mode (read-only / dry-run) is always honored.
/// </summary>
/// <remarks>
///     Request bodies are mapped onto the generated models (so the wire shapes stay typed and validated), but
///     the requests are sent through <see cref="ZendeskKiotaRequests.SendForJsonAsync" /> (raw JSON passthrough)
///     wherever the response payload matters: the generated ticket models mark the fields agents need as
///     read-only and drop them on re-serialization (<c>TicketObject</c> loses <c>id</c>/<c>created_at</c>/
///     <c>updated_at</c>/<c>tags</c>, <c>JobStatusObject</c> and <c>AttachmentObject</c> lose their state the
///     same way). The tag endpoints additionally need their documented JSON bodies attached manually — the
///     published spec models them body-less (and a DELETE-with-body cannot be expressed in OpenAPI at all).
///     Responses are then projected to <b>lean write confirmations</b>: the minimum an agent needs to verify the
///     outcome — identity fields plus, for updates, the server-state values of exactly the fields that were sent
///     (revealing trigger/business-rule overrides without a follow-up read). The complete record stays reachable
///     via <c>tickets_get</c>, job progress via <c>job_statuses_get</c>.
/// </remarks>
[McpServerToolType]
public sealed class ZendeskTicketWriteTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IMcpExecutionModeAccessor executionMode)
{
    /// <summary>
    ///     Serializer for the JSON payloads the generated builders cannot express (the tag endpoints' bodies and
    ///     the raw-passthrough fields carried via <c>AdditionalData</c>) and for the request-side field detection
    ///     of the update echo-of-change and the bulk dry-run digests: the curated models' snake_case
    ///     <see cref="JsonPropertyName" /> mappings produce the documented wire shape, and unset (<c>null</c>)
    ///     fields are omitted.
    /// </summary>
    private static readonly JsonSerializerOptions WriteJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Creates a Zendesk ticket.</summary>
    [McpServerTool(Name = "tickets_create", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Create a ticket. 'comment' becomes the description (effectively required); triggers/automations and " +
        "notifications fire. Attach files: put uploads_create tokens in comment.uploads. additional_tags/" +
        "remove_tags NOT supported here — use 'tags' to set the tag list. Historical data (no triggers/" +
        "notifications): use tickets_import. Returns {id, subject, status, created_at, audit_id}; full record via " +
        "tickets_get. Write op — read-only: rejected; dry-run: simulated (no changes).")]
    public Task<object> Create(
        [Description(
            "Ticket to create; unset (null) fields omitted. 'comment' becomes the description; set " +
            "comment.public=false for an internal-note description. status: new|open|pending|hold|solved|closed. " +
            "priority: low|normal|high|urgent. type: problem|incident|question|task. additional_tags/remove_tags " +
            "rejected — use 'tags'.")]
        ZendeskTicketWrite ticket,
        CancellationToken cancellationToken)
    {
        var action = $"create a ticket with subject '{ticket.Subject}'";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                ValidateNoTagDeltas(ticket, nameof(ticket));
                var request = zendesk.Api.V2.Tickets.ToPostRequestInformation(
                    new TicketCreateRequest { Ticket = MapTicket(ticket) });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildCreateConfirmation(json);
            },
            () =>
            {
                // Validated in dry-run too, so the simulation teaches the agent the same contract.
                ValidateNoTagDeltas(ticket, nameof(ticket));
                return new ZendeskDryRunResult
                {
                    Description = $"Dry run — no changes were made. This call would {action}.",
                    Request = ticket
                };
            });
    }

    /// <summary>Creates up to 100 Zendesk tickets as an async job.</summary>
    [McpServerTool(Name = "tickets_create_many", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Create up to 100 tickets as one async job. Returns queued job {id, status} — poll job_statuses_get by id " +
        "until completed; per-ticket outcomes (incl. partial failures) in the job's results_summary. " +
        "additional_tags/remove_tags NOT supported here — use 'tags' per ticket. Historical/backfill: prefer " +
        "tickets_import_many (skips triggers/notifications). Write op — read-only: rejected; dry-run: simulated " +
        "(no changes).")]
    public Task<object> CreateMany(
        [Description(
            "Tickets to create (1-100 per call). Same shape as tickets_create. status: new|open|pending|hold|" +
            "solved|closed. priority: low|normal|high|urgent. type: problem|incident|question|task. " +
            "additional_tags/remove_tags rejected — use 'tags'.")]
        ZendeskTicketWrite[] tickets,
        CancellationToken cancellationToken)
    {
        var action = $"create {tickets.Length} tickets";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                ValidateBulkCount(tickets.Length, nameof(tickets));
                foreach (var ticket in tickets) ValidateNoTagDeltas(ticket, nameof(tickets));
                var request = zendesk.Api.V2.Tickets.Create_many.ToPostRequestInformation(
                    new TicketsCreateRequest
                    {
                        Tickets = tickets.Select(ticket => (TicketObject)MapTicket(ticket)).ToList()
                    });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return LeanJobConfirmation(json);
            },
            () =>
            {
                ValidateBulkCount(tickets.Length, nameof(tickets));
                foreach (var ticket in tickets) ValidateNoTagDeltas(ticket, nameof(tickets));
                return ZendeskDryRunResult.ForBulk(action, "create", "tickets", tickets, WriteJsonOptions);
            });
    }

    // ── Single-action ticket writes (decomposed from the former composite tickets_update) ─────────────────────
    // Each sets exactly ONE aspect of a ticket, so a consuming agent can be granted individual actions via its
    // tool include-list (e.g. tickets_note_add without tickets_reply_public). All route through SetTicketFields →
    // the same PUT /tickets/{id} + execution-mode gate + echo-of-change confirmation.

    /// <summary>Adds a public reply (visible to the requester) to a ticket.</summary>
    [McpServerTool(Name = "tickets_reply_public", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Add a PUBLIC reply to a ticket — visible to the requester (customer-facing). For an agents-only comment " +
        "use tickets_note_add instead. Provide exactly one of body or htmlBody; attach files via uploads " +
        "(uploads_create tokens). Returns {id, updated_at, audit_id}. Write op — read-only: rejected; dry-run: " +
        "simulated (no changes).")]
    public Task<object> ReplyPublic(
        [Description("Numeric ticket id.")] long id,
        [Description("Plain-text reply. Provide exactly one of body or htmlBody.")]
        string? body = null,
        [Description("HTML reply. Provide exactly one of body or htmlBody.")]
        string? htmlBody = null,
        [Description("Optional upload tokens from uploads_create, to attach files.")]
        string[]? uploads = null,
        [Description("Optional; ticket's latest updated_at for optimistic-concurrency (409 on mismatch).")]
        DateTimeOffset? updatedStamp = null,
        CancellationToken cancellationToken = default)
        => SetTicketFields(id, CommentChange(body, htmlBody, uploads, true), updatedStamp,
            $"reply publicly to ticket {id}", cancellationToken, () => ValidateExactlyOneBody(body, htmlBody));

    /// <summary>Adds an internal note (agents only) to a ticket.</summary>
    [McpServerTool(Name = "tickets_note_add", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Add an INTERNAL note to a ticket — visible to agents only, NEVER to the requester. For a customer-facing " +
        "reply use tickets_reply_public instead. Provide exactly one of body or htmlBody; attach files via uploads " +
        "(uploads_create tokens). Returns {id, updated_at, audit_id}. Write op — read-only: rejected; dry-run: " +
        "simulated (no changes).")]
    public Task<object> NoteAdd(
        [Description("Numeric ticket id.")] long id,
        [Description("Plain-text note. Provide exactly one of body or htmlBody.")]
        string? body = null,
        [Description("HTML note. Provide exactly one of body or htmlBody.")]
        string? htmlBody = null,
        [Description("Optional upload tokens from uploads_create, to attach files.")]
        string[]? uploads = null,
        [Description("Optional; ticket's latest updated_at for optimistic-concurrency (409 on mismatch).")]
        DateTimeOffset? updatedStamp = null,
        CancellationToken cancellationToken = default)
        => SetTicketFields(id, CommentChange(body, htmlBody, uploads, false), updatedStamp,
            $"add an internal note to ticket {id}", cancellationToken, () => ValidateExactlyOneBody(body, htmlBody));

    /// <summary>Sets a ticket's status.</summary>
    [McpServerTool(Name = "tickets_status_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Set a ticket's status. status: new|open|pending|hold|solved|closed (closed is terminal — cannot reopen). " +
        "When the account uses custom statuses, pass customStatusId for the exact status ('status' sets only the " +
        "category). Returns {id, updated_at, status}. Write op — read-only: rejected; dry-run: simulated.")]
    public Task<object> StatusSet(
        [Description("Numeric ticket id.")] long id,
        [Description("new|open|pending|hold|solved|closed.")]
        string status,
        [Description("Optional exact custom status id (from custom_statuses_list); 'status' sets only the category.")]
        long? customStatusId = null,
        [Description("Optional; ticket's latest updated_at for optimistic-concurrency (409 on mismatch).")]
        DateTimeOffset? updatedStamp = null,
        CancellationToken cancellationToken = default)
        => SetTicketFields(id, new ZendeskTicketWrite { Status = status, CustomStatusId = customStatusId },
            updatedStamp, $"set ticket {id} status to '{status}'", cancellationToken);

    /// <summary>Sets a ticket's priority.</summary>
    [McpServerTool(Name = "tickets_priority_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Set a ticket's priority: low|normal|high|urgent. Returns {id, updated_at, priority}. Write op — " +
        "read-only: rejected; dry-run: simulated.")]
    public Task<object> PrioritySet(
        [Description("Numeric ticket id.")] long id,
        [Description("low|normal|high|urgent.")]
        string priority,
        [Description("Optional; ticket's latest updated_at for optimistic-concurrency (409 on mismatch).")]
        DateTimeOffset? updatedStamp = null,
        CancellationToken cancellationToken = default)
        => SetTicketFields(id, new ZendeskTicketWrite { Priority = priority }, updatedStamp,
            $"set ticket {id} priority to '{priority}'", cancellationToken);

    /// <summary>Sets a ticket's type.</summary>
    [McpServerTool(Name = "tickets_type_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Set a ticket's type: problem|incident|question|task. Returns {id, updated_at, type}. Write op — " +
        "read-only: rejected; dry-run: simulated.")]
    public Task<object> TypeSet(
        [Description("Numeric ticket id.")] long id,
        [Description("problem|incident|question|task.")]
        string type,
        [Description("Optional; ticket's latest updated_at for optimistic-concurrency (409 on mismatch).")]
        DateTimeOffset? updatedStamp = null,
        CancellationToken cancellationToken = default)
        => SetTicketFields(id, new ZendeskTicketWrite { Type = type }, updatedStamp,
            $"set ticket {id} type to '{type}'", cancellationToken);

    /// <summary>Assigns a ticket to an agent (optionally also its group).</summary>
    [McpServerTool(Name = "tickets_assignee_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Assign a ticket to an agent by user id (optionally also set its group). To route to a group without " +
        "picking an agent use tickets_group_set. Returns {id, updated_at, assignee_id}. Write op — read-only: " +
        "rejected; dry-run: simulated.")]
    public Task<object> AssigneeSet(
        [Description("Numeric ticket id.")] long id,
        [Description("Numeric user id of the agent to assign.")]
        long assigneeId,
        [Description("Optional group id to set alongside the assignee.")]
        long? groupId = null,
        [Description("Optional; ticket's latest updated_at for optimistic-concurrency (409 on mismatch).")]
        DateTimeOffset? updatedStamp = null,
        CancellationToken cancellationToken = default)
        => SetTicketFields(id, new ZendeskTicketWrite { AssigneeId = assigneeId, GroupId = groupId },
            updatedStamp, $"assign ticket {id} to agent {assigneeId}", cancellationToken);

    /// <summary>Routes a ticket to a group.</summary>
    [McpServerTool(Name = "tickets_group_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Route a ticket to a group by group id (without assigning a specific agent). Returns {id, updated_at, " +
        "group_id}. Write op — read-only: rejected; dry-run: simulated.")]
    public Task<object> GroupSet(
        [Description("Numeric ticket id.")] long id,
        [Description("Numeric group id.")] long groupId,
        [Description("Optional; ticket's latest updated_at for optimistic-concurrency (409 on mismatch).")]
        DateTimeOffset? updatedStamp = null,
        CancellationToken cancellationToken = default)
        => SetTicketFields(id, new ZendeskTicketWrite { GroupId = groupId }, updatedStamp,
            $"route ticket {id} to group {groupId}", cancellationToken);

    /// <summary>Sets a ticket's requester.</summary>
    [McpServerTool(Name = "tickets_requester_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Change a ticket's requester (the customer the ticket is on behalf of) by user id. Returns {id, " +
        "updated_at, requester_id}. Write op — read-only: rejected; dry-run: simulated.")]
    public Task<object> RequesterSet(
        [Description("Numeric ticket id.")] long id,
        [Description("Numeric user id of the new requester.")]
        long requesterId,
        [Description("Optional; ticket's latest updated_at for optimistic-concurrency (409 on mismatch).")]
        DateTimeOffset? updatedStamp = null,
        CancellationToken cancellationToken = default)
        => SetTicketFields(id, new ZendeskTicketWrite { RequesterId = requesterId }, updatedStamp,
            $"set the requester of ticket {id} to user {requesterId}", cancellationToken);

    /// <summary>Sets a ticket's organization.</summary>
    [McpServerTool(Name = "tickets_organization_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Set a ticket's organization by organization id. Returns {id, updated_at, organization_id}. Write op — " +
        "read-only: rejected; dry-run: simulated.")]
    public Task<object> OrganizationSet(
        [Description("Numeric ticket id.")] long id,
        [Description("Numeric organization id.")]
        long organizationId,
        [Description("Optional; ticket's latest updated_at for optimistic-concurrency (409 on mismatch).")]
        DateTimeOffset? updatedStamp = null,
        CancellationToken cancellationToken = default)
        => SetTicketFields(id, new ZendeskTicketWrite { OrganizationId = organizationId }, updatedStamp,
            $"set the organization of ticket {id} to {organizationId}", cancellationToken);

    /// <summary>Sets a ticket's form.</summary>
    [McpServerTool(Name = "tickets_form_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Set a ticket's form by ticket form id (see forms_list). Returns {id, updated_at, ticket_form_id}. Write " +
        "op — read-only: rejected; dry-run: simulated.")]
    public Task<object> FormSet(
        [Description("Numeric ticket id.")] long id,
        [Description("Numeric ticket form id (from forms_list).")]
        long ticketFormId,
        [Description("Optional; ticket's latest updated_at for optimistic-concurrency (409 on mismatch).")]
        DateTimeOffset? updatedStamp = null,
        CancellationToken cancellationToken = default)
        => SetTicketFields(id, new ZendeskTicketWrite { TicketFormId = ticketFormId }, updatedStamp,
            $"set the form of ticket {id} to {ticketFormId}", cancellationToken);

    /// <summary>Sets a ticket's custom field values.</summary>
    [McpServerTool(Name = "tickets_custom_fields_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Set custom ticket-field values — an array of {id, value}; the value shape depends on the field type " +
        "(text=string, checkbox=bool, date=\"YYYY-MM-DD\", tagger=option tag value, multiselect=array of tag " +
        "values, integer/decimal=number, lookup=related record id). Get ids/types from ticket_fields_list. Only " +
        "the listed fields change. Returns {id, updated_at, custom_fields} (only the fields you set). Write op — " +
        "read-only: rejected; dry-run: simulated.")]
    public Task<object> CustomFieldsSet(
        [Description("Numeric ticket id.")] long id,
        [Description("Custom field values to set: array of {id: <field id>, value: <type-dependent>}.")]
        ZendeskCustomFieldWrite[] fields,
        [Description("Optional; ticket's latest updated_at for optimistic-concurrency (409 on mismatch).")]
        DateTimeOffset? updatedStamp = null,
        CancellationToken cancellationToken = default)
        => SetTicketFields(id, new ZendeskTicketWrite { CustomFields = fields }, updatedStamp,
            $"set {fields.Length} custom field(s) on ticket {id}", cancellationToken);

    /// <summary>Sets a ticket's collaborators (CCs).</summary>
    [McpServerTool(Name = "tickets_collaborators_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Set a ticket's collaborators (CCs) — REPLACES the whole collaborator list with the given user ids (pass " +
        "an empty list to clear). Returns {id, updated_at, collaborator_ids}. Write op — read-only: rejected; " +
        "dry-run: simulated.")]
    public Task<object> CollaboratorsSet(
        [Description("Numeric ticket id.")] long id,
        [Description("User ids to set as collaborators/CCs (replaces the whole list; empty clears it).")]
        long[] collaboratorIds,
        [Description("Optional; ticket's latest updated_at for optimistic-concurrency (409 on mismatch).")]
        DateTimeOffset? updatedStamp = null,
        CancellationToken cancellationToken = default)
        => SetTicketFields(id, new ZendeskTicketWrite { CollaboratorIds = collaboratorIds }, updatedStamp,
            $"set the collaborators of ticket {id}", cancellationToken);

    /// <summary>Sets a ticket's due date (task tickets only).</summary>
    [McpServerTool(Name = "tickets_due_at_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Set a ticket's due date (ISO8601). Only honored when the ticket type is 'task' (set via tickets_type_set). " +
        "Returns {id, updated_at, due_at}. Write op — read-only: rejected; dry-run: simulated.")]
    public Task<object> DueAtSet(
        [Description("Numeric ticket id.")] long id,
        [Description("Due timestamp (ISO8601). Honored only for type=task.")]
        DateTimeOffset dueAt,
        [Description("Optional; ticket's latest updated_at for optimistic-concurrency (409 on mismatch).")]
        DateTimeOffset? updatedStamp = null,
        CancellationToken cancellationToken = default)
        => SetTicketFields(id, new ZendeskTicketWrite { DueAt = dueAt }, updatedStamp,
            $"set the due date of ticket {id}", cancellationToken);

    /// <summary>Sets a ticket's subject.</summary>
    [McpServerTool(Name = "tickets_subject_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Set a ticket's subject line. Returns {id, updated_at, subject}. Write op — read-only: rejected; dry-run: " +
        "simulated.")]
    public Task<object> SubjectSet(
        [Description("Numeric ticket id.")] long id,
        [Description("New subject line.")] string subject,
        [Description("Optional; ticket's latest updated_at for optimistic-concurrency (409 on mismatch).")]
        DateTimeOffset? updatedStamp = null,
        CancellationToken cancellationToken = default)
        => SetTicketFields(id, new ZendeskTicketWrite { Subject = subject }, updatedStamp,
            $"set the subject of ticket {id}", cancellationToken);

    // ── Bulk single-action ticket writes (each applies ONE change to up to 100 tickets as an async job) ───────
    // The bulk counterparts of the sensitive/common single-action tools, kept as separate gateable names so a
    // bulk public reply cannot ride in through a catch-all bulk-update tool.

    /// <summary>Sets the status of up to 100 tickets as an async job.</summary>
    [McpServerTool(Name = "tickets_status_set_many", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Set the SAME status on up to 100 tickets as an async job. status: new|open|pending|hold|solved|closed. " +
        "Returns queued job {id, status} — poll job_statuses_get by id. Write op — read-only: rejected; dry-run: " +
        "simulated.")]
    public Task<object> StatusSetMany(
        [Description("Numeric ids of tickets to update (1-100 per call).")]
        long[] ids,
        [Description("new|open|pending|hold|solved|closed.")]
        string status,
        CancellationToken cancellationToken)
        => SetTicketFieldsMany(ids, new ZendeskTicketWrite { Status = status },
            $"set {ids.Length} tickets to status '{status}'", cancellationToken);

    /// <summary>Assigns up to 100 tickets to an agent as an async job.</summary>
    [McpServerTool(Name = "tickets_assignee_set_many", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Assign up to 100 tickets to the SAME agent as an async job (optionally also set their group). Returns " +
        "queued job {id, status} — poll job_statuses_get. Write op — read-only: rejected; dry-run: simulated.")]
    public Task<object> AssigneeSetMany(
        [Description("Numeric ids of tickets to update (1-100 per call).")]
        long[] ids,
        [Description("Numeric user id of the agent to assign.")]
        long assigneeId,
        [Description("Optional group id to set alongside the assignee.")]
        long? groupId = null,
        CancellationToken cancellationToken = default)
        => SetTicketFieldsMany(ids, new ZendeskTicketWrite { AssigneeId = assigneeId, GroupId = groupId },
            $"assign {ids.Length} tickets to agent {assigneeId}", cancellationToken);

    /// <summary>Routes up to 100 tickets to a group as an async job.</summary>
    [McpServerTool(Name = "tickets_group_set_many", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Route up to 100 tickets to the SAME group as an async job. Returns queued job {id, status} — poll " +
        "job_statuses_get. Write op — read-only: rejected; dry-run: simulated.")]
    public Task<object> GroupSetMany(
        [Description("Numeric ids of tickets to update (1-100 per call).")]
        long[] ids,
        [Description("Numeric group id.")] long groupId,
        CancellationToken cancellationToken)
        => SetTicketFieldsMany(ids, new ZendeskTicketWrite { GroupId = groupId },
            $"route {ids.Length} tickets to group {groupId}", cancellationToken);

    /// <summary>Adds tags to up to 100 tickets as an async job.</summary>
    [McpServerTool(Name = "tickets_tags_add_many", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Add the given tags to up to 100 tickets as an async job WITHOUT disturbing their existing tags — the " +
        "bulk tag-append path (also the way to edit tags on closed tickets). Returns queued job {id, status} — " +
        "poll job_statuses_get. Write op — read-only: rejected; dry-run: simulated.")]
    public Task<object> TagsAddMany(
        [Description("Numeric ids of tickets to update (1-100 per call).")]
        long[] ids,
        [Description("Tags to add to every listed ticket.")]
        string[] tags,
        CancellationToken cancellationToken)
        => SetTicketFieldsMany(ids, new ZendeskTicketWrite { AdditionalTags = tags },
            $"add tags [{string.Join(", ", tags)}] to {ids.Length} tickets", cancellationToken);

    /// <summary>Removes tags from up to 100 tickets as an async job.</summary>
    [McpServerTool(Name = "tickets_tags_remove_many", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Remove the given tags from up to 100 tickets as an async job WITHOUT disturbing their other tags (also " +
        "the way to edit tags on closed tickets). Returns queued job {id, status} — poll job_statuses_get. Write " +
        "op — read-only: rejected; dry-run: simulated.")]
    public Task<object> TagsRemoveMany(
        [Description("Numeric ids of tickets to update (1-100 per call).")]
        long[] ids,
        [Description("Tags to remove from every listed ticket.")]
        string[] tags,
        CancellationToken cancellationToken)
        => SetTicketFieldsMany(ids, new ZendeskTicketWrite { RemoveTags = tags },
            $"remove tags [{string.Join(", ", tags)}] from {ids.Length} tickets", cancellationToken);

    /// <summary>Adds an internal note to up to 100 tickets as an async job.</summary>
    [McpServerTool(Name = "tickets_note_add_many", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Add the SAME internal note (agents only) to up to 100 tickets as an async job. For customer-facing bulk " +
        "replies use tickets_reply_public_many. Provide exactly one of body or htmlBody. Returns queued job {id, " +
        "status} — poll job_statuses_get. Write op — read-only: rejected; dry-run: simulated.")]
    public Task<object> NoteAddMany(
        [Description("Numeric ids of tickets to update (1-100 per call).")]
        long[] ids,
        [Description("Plain-text note. Provide exactly one of body or htmlBody.")]
        string? body = null,
        [Description("HTML note. Provide exactly one of body or htmlBody.")]
        string? htmlBody = null,
        CancellationToken cancellationToken = default)
        => SetTicketFieldsMany(ids, CommentChange(body, htmlBody, null, false),
            $"add an internal note to {ids.Length} tickets", cancellationToken,
            () => ValidateExactlyOneBody(body, htmlBody));

    /// <summary>Adds a public reply to up to 100 tickets as an async job.</summary>
    [McpServerTool(Name = "tickets_reply_public_many", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Add the SAME PUBLIC reply (visible to requesters) to up to 100 tickets as an async job — customer-facing, " +
        "gate carefully. For internal bulk notes use tickets_note_add_many. Provide exactly one of body or " +
        "htmlBody. Returns queued job {id, status} — poll job_statuses_get. Write op — read-only: rejected; " +
        "dry-run: simulated.")]
    public Task<object> ReplyPublicMany(
        [Description("Numeric ids of tickets to update (1-100 per call).")]
        long[] ids,
        [Description("Plain-text reply. Provide exactly one of body or htmlBody.")]
        string? body = null,
        [Description("HTML reply. Provide exactly one of body or htmlBody.")]
        string? htmlBody = null,
        CancellationToken cancellationToken = default)
        => SetTicketFieldsMany(ids, CommentChange(body, htmlBody, null, true),
            $"reply publicly to {ids.Length} tickets", cancellationToken,
            () => ValidateExactlyOneBody(body, htmlBody));

    /// <summary>Sets custom field values on up to 100 tickets as an async job.</summary>
    [McpServerTool(Name = "tickets_custom_fields_set_many", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Set the SAME custom field values on up to 100 tickets as an async job — array of {id, value} (see " +
        "tickets_custom_fields_set for the value shapes). Returns queued job {id, status} — poll job_statuses_get. " +
        "Write op — read-only: rejected; dry-run: simulated.")]
    public Task<object> CustomFieldsSetMany(
        [Description("Numeric ids of tickets to update (1-100 per call).")]
        long[] ids,
        [Description("Custom field values to set on every listed ticket: array of {id, value}.")]
        ZendeskCustomFieldWrite[] fields,
        CancellationToken cancellationToken)
        => SetTicketFieldsMany(ids, new ZendeskTicketWrite { CustomFields = fields },
            $"set {fields.Length} custom field(s) on {ids.Length} tickets", cancellationToken);

    /// <summary>Soft-deletes a Zendesk ticket.</summary>
    [McpServerTool(Name = "tickets_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Soft-delete a ticket. Recoverable ~30 days via tickets_restore, then purged. Rate-limited to 400 ticket " +
        "deletions/minute. Irreversible removal: tickets_delete_permanently. Returns acknowledgement carrying the " +
        "ticket id. Write op — read-only: rejected; dry-run: simulated (no changes).")]
    public Task<object> Delete(
        [Description("Numeric ticket id.")] long id,
        CancellationToken cancellationToken)
    {
        var action = $"soft-delete ticket {id}";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                await zendesk.Api.V2.Tickets[id].DeleteAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return Acknowledge(action, id);
            },
            new { id });
    }

    /// <summary>Soft-deletes up to 100 tickets as an async job.</summary>
    [McpServerTool(Name = "tickets_delete_many", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Soft-delete up to 100 tickets as an async job. Recoverable ~30 days via tickets_restore(_many). Returns " +
        "queued job {id, status} — poll job_statuses_get by id until completed. Write op — read-only: rejected; " +
        "dry-run: simulated (no changes).")]
    public Task<object> DeleteMany(
        [Description("Numeric ids of tickets to soft-delete (1-100 per call).")]
        long[] ids,
        CancellationToken cancellationToken)
    {
        var action = $"soft-delete {ids.Length} tickets";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                ValidateBulkCount(ids.Length, nameof(ids));
                var request =
                    zendesk.Api.V2.Tickets.Destroy_many.ToDeleteRequestInformation(cfg =>
                        cfg.QueryParameters.Ids = string.Join(',', ids));
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return LeanJobConfirmation(json);
            },
            () =>
            {
                ValidateBulkCount(ids.Length, nameof(ids));
                return ZendeskDryRunResult.ForBulk(action, "delete", "tickets", ids.Cast<object?>());
            });
    }

    /// <summary>Merges source tickets into a target ticket as an async job.</summary>
    [McpServerTool(Name = "tickets_merge", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Merge source tickets into a target as an async job — sources are closed and their conversations folded " +
        "into the target; cannot be undone. Merge comments default to private (internal); set *_comment_is_public " +
        "flags to make them visible to requesters. Returns merge job {id, status} — poll job_statuses_get by id " +
        "until completed, then verify the surviving ticket via tickets_get. Write op — read-only: rejected; " +
        "dry-run: simulated (no changes).")]
    public Task<object> Merge(
        [Description("Numeric id of the surviving ticket (receives the merged conversations).")]
        long targetTicketId,
        [Description("Numeric ids of tickets to merge into the target; closed by the merge.")]
        long[] sourceTicketIds,
        [Description("Optional comment placed on the target ticket describing the merge.")]
        string? targetComment = null,
        [Description("Optional comment placed on each source ticket describing the merge.")]
        string? sourceComment = null,
        [Description("Optional; target-ticket merge comment public? Zendesk defaults to private.")]
        bool? targetCommentIsPublic = null,
        [Description("Optional; source-ticket merge comment public? Zendesk defaults to private.")]
        bool? sourceCommentIsPublic = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"merge tickets: {string.Join(", ", sourceTicketIds)} (sources) into {targetTicketId} (target)",
            async () =>
            {
                // Merge has NO documented id cap (unlike the 100-item bulk endpoints) — only reject an empty list.
                if (sourceTicketIds.Length == 0)
                    throw new ArgumentException("At least one source ticket id is required.",
                        nameof(sourceTicketIds));
                // QUIRK: the merge payload is a BARE object — no "ticket" envelope. The generated model types
                // "ids" as int — Zendesk ticket ids are 64-bit, so they ride via AdditionalData instead.
                var payload = new TicketMergeInput
                {
                    TargetComment = targetComment,
                    SourceComment = sourceComment,
                    TargetCommentIsPublic = targetCommentIsPublic,
                    SourceCommentIsPublic = sourceCommentIsPublic
                };
                payload.AdditionalData["ids"] = JsonSerializer.SerializeToElement(sourceTicketIds);
                var request = zendesk.Api.V2.Tickets[targetTicketId].Merge.ToPostRequestInformation(payload);
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return LeanJobConfirmation(json);
            },
            new
            {
                targetTicketId,
                sourceTicketIds,
                targetComment,
                sourceComment,
                targetCommentIsPublic,
                sourceCommentIsPublic
            });

    /// <summary>Marks a ticket as spam and suspends its requester.</summary>
    [McpServerTool(Name = "tickets_mark_spam", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Mark a ticket as spam AND suspend its requester — the requester cannot submit tickets until unsuspended, " +
        "so verify the sender really is a spammer first. Returns acknowledgement carrying the ticket id. Write op " +
        "— read-only: rejected; dry-run: simulated (no changes).")]
    public Task<object> MarkSpam(
        [Description("Numeric ticket id.")] long id,
        CancellationToken cancellationToken)
    {
        var action = $"mark ticket {id} as spam and suspend its requester";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                await zendesk.Api.V2.Tickets[id].Mark_as_spam.PutAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return Acknowledge(action, id);
            },
            new { id });
    }

    /// <summary>Marks up to 100 tickets as spam as an async job.</summary>
    [McpServerTool(Name = "tickets_mark_spam_many", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Mark up to 100 tickets as spam as an async job, suspending their requesters. Returns queued job {id, " +
        "status} — poll job_statuses_get by id until completed. Write op — read-only: rejected; dry-run: " +
        "simulated (no changes).")]
    public Task<object> MarkSpamMany(
        [Description("Numeric ids of tickets to mark as spam (1-100 per call).")]
        long[] ids,
        CancellationToken cancellationToken)
    {
        var action = $"mark {ids.Length} tickets as spam";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                ValidateBulkCount(ids.Length, nameof(ids));
                var request =
                    zendesk.Api.V2.Tickets.Mark_many_as_spam.ToPutRequestInformation(cfg =>
                        cfg.QueryParameters.Ids = string.Join(',', ids));
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return LeanJobConfirmation(json);
            },
            () =>
            {
                ValidateBulkCount(ids.Length, nameof(ids));
                return ZendeskDryRunResult.ForBulk(action, "mark_spam", "tickets", ids.Cast<object?>());
            });
    }

    /// <summary>Restores a soft-deleted ticket.</summary>
    [McpServerTool(Name = "tickets_restore", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Restore a soft-deleted ticket (undoes tickets_delete within the ~30-day recovery window). Returns " +
        "acknowledgement carrying the ticket id. Write op — read-only: rejected; dry-run: simulated (no " +
        "changes).")]
    public Task<object> Restore(
        [Description("Numeric id of the soft-deleted ticket.")]
        long id,
        CancellationToken cancellationToken)
    {
        var action = $"restore soft-deleted ticket {id}";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                await zendesk.Api.V2.Deleted_tickets[id].Restore.PutAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return Acknowledge(action, id);
            },
            new { id });
    }

    /// <summary>Restores up to 100 soft-deleted tickets.</summary>
    [McpServerTool(Name = "tickets_restore_many", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Restore up to 100 soft-deleted tickets in one call. Unlike most bulk ticket ops this endpoint is " +
        "synchronous — no job created. Returns acknowledgement carrying the restored ids. Write op — read-only: " +
        "rejected; dry-run: simulated (no changes).")]
    public Task<object> RestoreMany(
        [Description("Numeric ids of soft-deleted tickets to restore (1-100 per call).")]
        long[] ids,
        CancellationToken cancellationToken)
    {
        var action = $"restore {ids.Length} soft-deleted tickets";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                ValidateBulkCount(ids.Length, nameof(ids));
                await zendesk.Api.V2.Deleted_tickets.Restore_many.PutAsync(
                        cfg => cfg.QueryParameters.Ids = string.Join(',', ids), cancellationToken)
                    .ConfigureAwait(false);
                return Acknowledge(action, ids: ids);
            },
            () =>
            {
                ValidateBulkCount(ids.Length, nameof(ids));
                return ZendeskDryRunResult.ForBulk(action, "restore", "tickets", ids.Cast<object?>());
            });
    }

    /// <summary>Permanently deletes an already soft-deleted ticket (irreversible).</summary>
    [McpServerTool(Name = "tickets_delete_permanently", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "PERMANENTLY delete a ticket already soft-deleted (tickets_delete first). IRREVERSIBLE — cannot be " +
        "recovered. Async even for one ticket: returns queued job {id, status} — poll job_statuses_get by id " +
        "until completed. Write op — read-only: rejected; dry-run: simulated (no changes).")]
    public Task<object> DeletePermanently(
        [Description("Numeric id of the already soft-deleted ticket.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"PERMANENTLY delete ticket {id} (irreversible)",
            async () =>
            {
                // QUIRK: async job_status even for a single ticket.
                var request = zendesk.Api.V2.Deleted_tickets[id].ToDeleteRequestInformation();
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return LeanJobConfirmation(json);
            },
            new { id });

    /// <summary>Permanently deletes up to 100 soft-deleted tickets as an async job (irreversible).</summary>
    [McpServerTool(Name = "tickets_delete_permanently_many", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "PERMANENTLY delete up to 100 already soft-deleted tickets as an async job. IRREVERSIBLE — cannot be " +
        "recovered. Returns queued job {id, status} — poll job_statuses_get by id until completed. Write op — " +
        "read-only: rejected; dry-run: simulated (no changes).")]
    public Task<object> DeletePermanentlyMany(
        [Description("Numeric ids of already soft-deleted tickets (1-100 per call).")]
        long[] ids,
        CancellationToken cancellationToken)
    {
        var action = $"PERMANENTLY delete {ids.Length} tickets (irreversible)";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                ValidateBulkCount(ids.Length, nameof(ids));
                var request =
                    zendesk.Api.V2.Deleted_tickets.Destroy_many.ToDeleteRequestInformation(cfg =>
                        cfg.QueryParameters.Ids = string.Join(',', ids));
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return LeanJobConfirmation(json);
            },
            () =>
            {
                ValidateBulkCount(ids.Length, nameof(ids));
                return ZendeskDryRunResult.ForBulk(action, "delete_permanently", "tickets", ids.Cast<object?>());
            });
    }

    /// <summary>Replaces a ticket's whole tag set.</summary>
    [McpServerTool(Name = "tickets_tags_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "REPLACE a ticket's whole tag set with the given tags — any tag not in the list is removed. Add/remove " +
        "specific tags without touching the rest: tickets_tags_add/tickets_tags_remove. Does not work on closed " +
        "tickets (use tickets_tags_add_many/tickets_tags_remove_many for those). Returns the resulting tag " +
        "list. Write op — read-only: rejected; dry-run: simulated (no changes).")]
    public Task<object> TagsSet(
        [Description("Numeric ticket id.")] long ticketId,
        [Description("Complete new tag set; existing tags not listed here are removed.")]
        string[] tags,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"replace the tag set of ticket {ticketId} with [{string.Join(", ", tags)}]",
            () => SendTagsAsync(ticketId, tags, null,
                builder => builder.ToPostRequestInformation(), cancellationToken),
            new { ticketId, tags });

    /// <summary>Adds tags to a ticket without removing existing ones.</summary>
    [McpServerTool(Name = "tickets_tags_add", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Add tags to a ticket without touching existing tags. Optionally pass updatedStamp (ticket's latest " +
        "updated_at from tickets_get) for optimistic-concurrency: if the ticket changed meanwhile, Zendesk " +
        "rejects with 409 Conflict instead of silently overwriting — re-read and retry. Does not work on closed " +
        "tickets. Returns the resulting tag list. Write op — read-only: rejected; dry-run: simulated (no " +
        "changes).")]
    public Task<object> TagsAdd(
        [Description("Numeric ticket id.")] long ticketId,
        [Description("Tags to add.")] string[] tags,
        [Description(
            "Optional; ticket's latest updated_at for safe-update collision protection (409 on mismatch).")]
        DateTimeOffset? updatedStamp = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"add tags [{string.Join(", ", tags)}] to ticket {ticketId}",
            () => SendTagsAsync(ticketId, tags, updatedStamp,
                builder => builder.ToPutRequestInformation(), cancellationToken),
            new { ticketId, tags, updatedStamp });

    /// <summary>Removes tags from a ticket.</summary>
    [McpServerTool(Name = "tickets_tags_remove", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Remove the given tags from a ticket, leaving other tags in place. Optionally pass updatedStamp (ticket's " +
        "latest updated_at from tickets_get) for optimistic-concurrency: a concurrent change makes the call fail " +
        "409 Conflict instead of silently overwriting — re-read and retry. Does not work on closed tickets. " +
        "Returns the resulting tag list. Write op — read-only: rejected; dry-run: simulated (no changes).")]
    public Task<object> TagsRemove(
        [Description("Numeric ticket id.")] long ticketId,
        [Description("Tags to remove.")] string[] tags,
        [Description(
            "Optional; ticket's latest updated_at for safe-update collision protection (409 on mismatch).")]
        DateTimeOffset? updatedStamp = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"remove tags [{string.Join(", ", tags)}] from ticket {ticketId}",
            // A DELETE with a JSON body — unusual, but that is the documented shape of the tag-removal endpoint.
            () => SendTagsAsync(ticketId, tags, updatedStamp,
                builder => builder.ToDeleteRequestInformation(), cancellationToken),
            new { ticketId, tags, updatedStamp });

    /// <summary>Makes a public ticket comment private.</summary>
    [McpServerTool(Name = "tickets_comments_make_private", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Make a public ticket comment private (internal note). ONE-WAY: Zendesk has no make-public — the only way " +
        "back is deleting and re-adding the content as a new public comment. Find comment ids with " +
        "tickets_comments_list. Returns acknowledgement carrying the comment id. Write op — read-only: rejected; " +
        "dry-run: simulated (no changes).")]
    public Task<object> CommentMakePrivate(
        [Description("Numeric ticket id.")] long ticketId,
        [Description("Numeric id of the comment to make private.")]
        long commentId,
        CancellationToken cancellationToken)
    {
        var action = $"make comment {commentId} on ticket {ticketId} private";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                await zendesk.Api.V2.Tickets[ticketId].Comments[commentId].Make_private
                    .PutAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                return Acknowledge(action, commentId);
            },
            new { ticketId, commentId });
    }

    /// <summary>Permanently redacts a comment attachment (irreversible).</summary>
    [McpServerTool(Name = "tickets_comments_attachment_redact", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "PERMANENTLY redact an attachment on a ticket comment, replacing the file with an empty 'redacted.txt'. " +
        "IRREVERSIBLE — original cannot be recovered — and not possible once the ticket is closed. Find comment " +
        "and attachment ids with tickets_comments_list. Returns the redacted attachment summary (id, file_name, " +
        "content_type, size). Write op — read-only: rejected; dry-run: simulated (no changes).")]
    public Task<object> CommentAttachmentRedact(
        [Description("Numeric ticket id.")] long ticketId,
        [Description("Numeric id of the comment carrying the attachment.")]
        long commentId,
        [Description("Numeric id of the attachment to redact.")]
        long attachmentId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"permanently redact attachment {attachmentId} on comment {commentId} of ticket {ticketId} (irreversible)",
            async () =>
            {
                var request = zendesk.Api.V2.Tickets[ticketId].Comments[commentId].Attachments[attachmentId]
                    .Redact.ToPutRequestInformation();
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildAttachmentConfirmation(json);
            },
            new { ticketId, commentId, attachmentId });

    /// <summary>Imports a historical ticket (admin-only; no triggers/notifications).</summary>
    [McpServerTool(Name = "tickets_import", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Import a HISTORICAL ticket (admin-only) — unlike tickets_create, accepts a whole comment conversation " +
        "and historical created_at/updated_at/solved_at timestamps, and skips triggers, notifications, metrics, " +
        "SLAs. archiveImmediately=true sends closed tickets straight to the archive (recommended for backfills). " +
        "Returns {id, subject, status, created_at}; full record via tickets_get. Write op — read-only: rejected; " +
        "dry-run: simulated (no changes).")]
    public Task<object> Import(
        [Description(
            "Historical ticket to import. status: new|open|pending|hold|solved|closed. comments: each carries " +
            "author_id, body OR html_body, public (defaults to true — set false for an internal note), and its own " +
            "historical created_at. created_at/updated_at/solved_at accept historical ISO8601 timestamps but not " +
            "before 1970 or in the future.")]
        ZendeskTicketImport ticket,
        [Description("Send the ticket directly to the archive after import (only valid for closed tickets).")]
        bool archiveImmediately = false,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"import a historical ticket with subject '{ticket.Subject}'",
            async () =>
            {
                var request = zendesk.Api.V2.Imports.Tickets.ToPostRequestInformation(
                    new TicketImportRequest { Ticket = MapImport(ticket) },
                    cfg =>
                    {
                        // Parity with the old client: the parameter is only sent when set.
                        if (archiveImmediately) cfg.QueryParameters.ArchiveImmediately = true;
                    });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildCreateConfirmation(json);
            },
            new { ticket, archiveImmediately });

    /// <summary>Imports up to 100 historical tickets as an async job (admin-only).</summary>
    [McpServerTool(Name = "tickets_import_many", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Import up to 100 HISTORICAL tickets as an async job (admin-only) — bulk form of tickets_import: whole " +
        "comment conversations and historical timestamps accepted; triggers, notifications, metrics, SLAs " +
        "skipped. archiveImmediately=true sends closed tickets straight to the archive. Returns queued job {id, " +
        "status} — poll job_statuses_get by id until completed. Write op — read-only: rejected; dry-run: " +
        "simulated (no changes).")]
    public Task<object> ImportMany(
        [Description(
            "Historical tickets to import (1-100 per call). Same shape as tickets_import: status is new|open|" +
            "pending|hold|solved|closed; comments each carry author_id, body OR html_body, public (defaults to " +
            "true), and their own created_at; created_at/updated_at/solved_at accept historical ISO8601 timestamps " +
            "but not before 1970 or in the future.")]
        ZendeskTicketImport[] tickets,
        [Description("Send the tickets directly to the archive after import (only valid for closed tickets).")]
        bool archiveImmediately = false,
        CancellationToken cancellationToken = default)
    {
        var action = $"import {tickets.Length} historical tickets";
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                ValidateBulkCount(tickets.Length, nameof(tickets));
                var request = zendesk.Api.V2.Imports.Tickets.Create_many.ToPostRequestInformation(
                    new TicketBulkImportRequest { Tickets = tickets.Select(MapImport).ToList() },
                    cfg =>
                    {
                        // Parity with the old client: the parameter is only sent when set.
                        if (archiveImmediately) cfg.QueryParameters.ArchiveImmediately = true;
                    });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return LeanJobConfirmation(json);
            },
            () =>
            {
                ValidateBulkCount(tickets.Length, nameof(tickets));
                return ZendeskDryRunResult.ForBulk(action, "import", "tickets", tickets, WriteJsonOptions);
            });
    }

    /// <summary>
    ///     Sends a tag mutation (<c>POST</c>/<c>PUT</c>/<c>DELETE</c> on <c>tickets/{id}/tags</c>). The generated
    ///     builder exposes these operations without their documented JSON bodies (and a DELETE-with-body cannot be
    ///     expressed by the spec at all), so the body is attached manually: <c>{"tags":[...]}</c>, plus
    ///     <c>updated_stamp</c> and the string <c>"true"</c> for <c>safe_update</c> when a stamp is supplied —
    ///     the exact wire shape prescribed by the tags docs
    ///     (https://developer.zendesk.com/api-reference/ticketing/ticket-management/tags/; spec-anomaly ledger
    ///     row in <c>src/ES.FX.Zendesk/OpenApi/README.md</c>).
    /// </summary>
    private async Task<JsonElement> SendTagsAsync(long ticketId, string[] tags, DateTimeOffset? updatedStamp,
        Func<TagsRequestBuilder, RequestInformation> toRequest,
        CancellationToken cancellationToken)
    {
        var request = toRequest(zendesk.Api.V2.Tickets[ticketId].Tags);
        // The tags docs pass safe_update as the string "true"; the stamp is the ticket's latest updated_at.
        var payload = new
        {
            tags,
            updated_stamp = updatedStamp,
            safe_update = updatedStamp is null ? null : "true"
        };
        request.SetStreamContent(new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(payload, WriteJsonOptions)),
            "application/json");
        return await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     The shared partial-update path for the single-action ticket setters: applies the optional
    ///     optimistic-lock stamp, sends the narrow write model as a <c>PUT /tickets/{id}</c>, and returns the lean
    ///     update confirmation (with echo-of-change). The <paramref name="validate" /> hook (when supplied) runs
    ///     INSIDE the execution-mode gate — so read-only mode still rejects first — mirroring the composite's
    ///     in-lambda validation. The dry-run echoes exactly the fields the setter set (nulls omitted).
    /// </summary>
    private Task<object> SetTicketFields(long id, ZendeskTicketWrite change, DateTimeOffset? updatedStamp,
        string action, CancellationToken cancellationToken, Action? validate = null)
    {
        var payload = updatedStamp is null ? change : change with { SafeUpdate = true, UpdatedStamp = updatedStamp };
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                validate?.Invoke();
                var request = zendesk.Api.V2.Tickets[id].ToPutRequestInformation(
                    new TicketUpdateRequest { Ticket = MapTicket(payload) });
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return BuildUpdateConfirmation(json, id, payload);
            },
            () =>
            {
                validate?.Invoke();
                var echo = new JsonObject { ["id"] = id };
                foreach (var (name, value) in JsonSerializer.SerializeToNode(payload, WriteJsonOptions)!.AsObject())
                    echo[name] = value?.DeepClone();
                return new ZendeskDryRunResult
                {
                    Description = $"Dry run — no changes were made. This call would {action}.",
                    Request = echo
                };
            });
    }

    /// <summary>
    ///     The shared bulk path for the single-action <c>*_set_many</c> ticket setters: applies ONE shared change
    ///     to up to 100 tickets via the <c>update_many</c> endpoint (targets in <c>?ids=</c>) and returns the async
    ///     job confirmation. Same in-lambda validation discipline as <see cref="SetTicketFields" />.
    /// </summary>
    private Task<object> SetTicketFieldsMany(long[] ids, ZendeskTicketWrite change, string action,
        CancellationToken cancellationToken, Action? validate = null)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, action,
            async () =>
            {
                ValidateBulkCount(ids.Length, nameof(ids));
                validate?.Invoke();
                var request = zendesk.Api.V2.Tickets.Update_many.ToPutRequestInformation(
                    new TicketsUpdateRequest { Ticket = MapTicket(change) },
                    cfg => cfg.QueryParameters.Ids = string.Join(',', ids));
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return LeanJobConfirmation(json);
            },
            () =>
            {
                ValidateBulkCount(ids.Length, nameof(ids));
                validate?.Invoke();
                return ZendeskDryRunResult.ForBulk(action, "update", "tickets", SharedChangeItems(ids, change));
            });

    /// <summary>Builds a comment-only ticket change (public reply vs internal note via <paramref name="isPublic" />).</summary>
    private static ZendeskTicketWrite CommentChange(string? body, string? htmlBody, string[]? uploads, bool isPublic) =>
        new()
        {
            Comment = new ZendeskTicketCommentWrite
            {
                Body = body, HtmlBody = htmlBody, Public = isPublic, Uploads = uploads
            }
        };

    /// <summary>Requires exactly one of <paramref name="body" /> / <paramref name="htmlBody" /> to be non-empty.</summary>
    private static void ValidateExactlyOneBody(string? body, string? htmlBody)
    {
        if (string.IsNullOrEmpty(body) == string.IsNullOrEmpty(htmlBody))
            throw new ArgumentException("Provide exactly one of body or htmlBody.", nameof(body));
    }

    /// <summary>Validates a bulk-operation item count (Zendesk accepts 1–100 items per bulk request).</summary>
    private static void ValidateBulkCount(int count, string paramName)
    {
        if (count is 0 or > 100)
            throw new ArgumentException(
                "Zendesk bulk operations accept 1–100 items per call; for more, split into batches of 100 and " +
                "submit one job per batch, then poll each.", paramName);
    }

    /// <summary>
    ///     Rejects tag-delta fields on the operations whose wire schema does not take them: the OAS models
    ///     <c>additional_tags</c>/<c>remove_tags</c> only on the <c>update_many</c> bulk schema
    ///     (<c>TicketsUpdateRequest</c>) — create, create_many and the single update take a plain ticket object,
    ///     where Zendesk silently ignores the unknown properties and the caller's intent is lost. Rejecting loudly
    ///     (instead of silently dropping the fields) points the agent at the operations that do the job.
    /// </summary>
    private static void ValidateNoTagDeltas(ZendeskTicketWrite ticket, string paramName)
    {
        if (ticket.AdditionalTags is not null || ticket.RemoveTags is not null)
            throw new ArgumentException(
                "additional_tags/remove_tags are not supported on this operation (Zendesk ignores them outside " +
                "the update_many bulk schema). Use tickets_tags_add / tickets_tags_remove for a single ticket, " +
                "or tickets_tags_add_many / tickets_tags_remove_many for bulk tag deltas.", paramName);
    }

    /// <summary>
    ///     Builds the completion acknowledgement for a body-less write, carrying the affected id(s) as structured
    ///     fields so the agent never has to parse them back out of the description prose.
    /// </summary>
    private static ZendeskWriteAcknowledgement Acknowledge(string action, long? id = null,
        IReadOnlyList<long>? ids = null) =>
        new() { Description = $"Zendesk accepted the request to {action}.", Id = id, Ids = ids };

    /// <summary>
    ///     The lean create/import confirmation — <c>{id, subject, status, created_at}</c> plus <c>audit_id</c>
    ///     when Zendesk echoed the creation audit. The audit member itself is stripped (it duplicates the request
    ///     the agent just sent); the complete record stays reachable via <c>tickets_get</c>.
    /// </summary>
    private static JsonElement BuildCreateConfirmation(JsonElement response)
    {
        var source = AsJsonObject(response);
        var confirmation = new JsonObject();
        if (source?["ticket"] is JsonObject ticket)
            CopyFields(ticket, confirmation, "id", "subject", "status", "created_at");
        AppendAuditId(source, confirmation);
        return JsonSerializer.SerializeToElement(confirmation);
    }

    /// <summary>
    ///     The lean update confirmation: <c>{id, updated_at, audit_id}</c> plus the <b>echo-of-change</b> — the
    ///     server-state values of exactly the fields present in the request. Comparing the echo against the
    ///     request reveals trigger/business-rule overrides without a follow-up <c>tickets_get</c>; request fields
    ///     with no ticket-state counterpart (comment, safe_update, updated_stamp) have nothing to echo and are
    ///     absent. The echoed audit member is stripped (<c>audit_id</c> survives) — the change detail stays
    ///     reachable via <c>tickets_audits_list</c>.
    /// </summary>
    private static JsonElement BuildUpdateConfirmation(JsonElement response, long id, ZendeskTicketWrite change)
    {
        var source = AsJsonObject(response);
        var ticket = source?["ticket"] as JsonObject;
        // Metadata first; the id falls back to the request fact if the response carried no ticket.
        var confirmation = new JsonObject { ["id"] = ticket?["id"]?.DeepClone() ?? JsonValue.Create(id) };
        if (ticket?["updated_at"] is { } updatedAt) confirmation["updated_at"] = updatedAt.DeepClone();
        AppendAuditId(source, confirmation);
        if (ticket is null) return JsonSerializer.SerializeToElement(confirmation);

        // Echo-of-change: the serialized request supplies the wire names of exactly the fields that were sent.
        var requested = JsonSerializer.SerializeToNode(change, WriteJsonOptions)!.AsObject();
        foreach (var (name, requestedValue) in requested)
        {
            if (confirmation.ContainsKey(name)) continue;
            if (name == "custom_fields")
            {
                // The server echoes EVERY custom field on the ticket; the confirmation must reveal only the one(s)
                // the request set — echoing the rest would leak unrelated, possibly sensitive, custom-field values.
                if (EchoRequestedCustomFields(requestedValue, ticket["custom_fields"]) is { } echoed)
                    confirmation["custom_fields"] = echoed;
                continue;
            }

            if (ticket[name] is { } value) confirmation[name] = value.DeepClone();
        }

        return JsonSerializer.SerializeToElement(confirmation);
    }

    /// <summary>
    ///     Filters the server's <c>custom_fields</c> array (which carries every field on the ticket) down to the
    ///     id(s) present in the request's <c>custom_fields</c> array, so the echo-of-change reveals only the fields
    ///     the caller actually set. Returns <c>null</c> when the shapes don't match or nothing lines up.
    /// </summary>
    private static JsonArray? EchoRequestedCustomFields(JsonNode? requested, JsonNode? serverFields)
    {
        if (requested is not JsonArray requestedArray || serverFields is not JsonArray serverArray) return null;

        var requestedIds = new HashSet<long>();
        foreach (var entry in requestedArray)
            if (entry is JsonObject obj && obj["id"] is JsonValue id && id.TryGetValue(out long fieldId))
                requestedIds.Add(fieldId);
        if (requestedIds.Count == 0) return null;

        var echoed = new JsonArray();
        foreach (var entry in serverArray)
            if (entry is JsonObject obj && obj["id"] is JsonValue id && id.TryGetValue(out long fieldId) &&
                requestedIds.Contains(fieldId))
                echoed.Add(entry.DeepClone());

        return echoed.Count > 0 ? echoed : null;
    }

    /// <summary>
    ///     The lean confirmation for a write that enqueues an async job: the job's summary row (effectively
    ///     <c>{id, status}</c> for a fresh job — see the job_status shape in <see cref="ZendeskLean" />) unwrapped
    ///     from its envelope, with the API self-link and null members gone. The id is everything an agent needs
    ///     to poll <c>job_statuses_get</c>. An unexpected response shape falls back to the full view so a
    ///     completed write's outcome is never hidden.
    /// </summary>
    private static JsonElement LeanJobConfirmation(JsonElement response)
    {
        var source = AsJsonObject(response);
        if (source?["job_status"] is JsonObject jobStatus)
            return JsonSerializer.SerializeToElement(ZendeskLean.SummarizeEntity("job_statuses", jobStatus)!);
        return response.ValueKind is JsonValueKind.Object ? ZendeskLean.ToFullView(response) : response;
    }

    /// <summary>
    ///     The lean redaction confirmation: the attachment summary row (id, file_name, content_type, size, ...)
    ///     unwrapped from its envelope — content URLs and thumbnails are gone. Falls back to the full view on an
    ///     unexpected response shape.
    /// </summary>
    private static JsonElement BuildAttachmentConfirmation(JsonElement response)
    {
        var source = AsJsonObject(response);
        if (source?["attachment"] is JsonObject attachment)
            return JsonSerializer.SerializeToElement(ZendeskLean.SummarizeEntity("attachments", attachment)!);
        return response.ValueKind is JsonValueKind.Object ? ZendeskLean.ToFullView(response) : response;
    }

    /// <summary>Keeps the created/updated audit's id on a confirmation while the audit member itself is stripped.</summary>
    private static void AppendAuditId(JsonObject? source, JsonObject confirmation)
    {
        if (source?["audit"] is JsonObject audit && audit["id"] is { } auditId)
            confirmation["audit_id"] = auditId.DeepClone();
    }

    /// <summary>Parses a response into a mutable node tree, or <c>null</c> for non-object payloads.</summary>
    private static JsonObject? AsJsonObject(JsonElement response) =>
        response.ValueKind is JsonValueKind.Object ? (JsonObject)JsonNode.Parse(response.GetRawText())! : null;

    /// <summary>Copies the allowlisted fields that are present and non-null, preserving the given order.</summary>
    private static void CopyFields(JsonObject source, JsonObject target, params string[] fields)
    {
        foreach (var field in fields)
            if (source[field] is { } value)
                target[field] = value.DeepClone();
    }

    /// <summary>
    ///     Expands the same-change bulk update into per-id items for the
    ///     <see cref="ZendeskDryRunResult.ForBulk" /> digest: each row carries the target id plus the shared
    ///     change's fields, so the digest still answers "which record, which fields" per item.
    /// </summary>
    private static IEnumerable<object?> SharedChangeItems(long[] ids, ZendeskTicketWrite change)
    {
        foreach (var id in ids)
        {
            var item = JsonSerializer.SerializeToNode(change, WriteJsonOptions)!.AsObject();
            item["id"] = id;
            yield return item;
        }
    }

    /// <summary>
    ///     Maps the curated ticket write model onto the generated <see cref="TicketUpdateInput" /> — the named
    ///     update input of the normalized spec, which extends the ticket object with the
    ///     <c>additional_tags</c>/<c>remove_tags</c> fields the curated model exposes. Used for create, the single
    ///     update, and the same-change bulk update (Kiota omits unassigned properties on the wire, matching the
    ///     omit-null serialization of the old client).
    /// </summary>
    private static TicketUpdateInput MapTicket(ZendeskTicketWrite ticket)
    {
        var input = new TicketUpdateInput
        {
            AdditionalTags = ticket.AdditionalTags?.ToList(),
            RemoveTags = ticket.RemoveTags?.ToList()
        };
        PopulateTicket(input, ticket);
        return input;
    }

    /// <summary>
    ///     Populates the shared ticket-object fields from the curated write model. The generated ticket object
    ///     has no writable <c>id</c> (the spec marks it read-only), so a set <see cref="ZendeskTicketWrite.Id" />
    ///     is carried via <c>AdditionalData</c> — required by the batch update and matching the wire shape of the
    ///     old omit-null serializer everywhere else.
    /// </summary>
    private static void PopulateTicket(TicketObject target, ZendeskTicketWrite ticket)
    {
        target.Subject = ticket.Subject;
        target.Comment = MapComment(ticket.Comment);
        target.Status = MapEnum<TicketObject_status>(ticket.Status, target.AdditionalData, "status");
        target.Priority = MapEnum<TicketObject_priority>(ticket.Priority, target.AdditionalData, "priority");
        target.Type = MapEnum<TicketObject_type>(ticket.Type, target.AdditionalData, "type");
        target.RequesterId = ticket.RequesterId;
        target.AssigneeId = ticket.AssigneeId;
        target.GroupId = ticket.GroupId;
        target.OrganizationId = ticket.OrganizationId;
        target.BrandId = ticket.BrandId;
        target.TicketFormId = ticket.TicketFormId;
        target.CustomStatusId = ticket.CustomStatusId;
        target.ProblemId = ticket.ProblemId;
        target.DueAt = ticket.DueAt;
        target.ExternalId = ticket.ExternalId;
        target.CollaboratorIds = ticket.CollaboratorIds?.Select(id => (long?)id).ToList();
        target.CustomFields = ticket.CustomFields?.Select(MapCustomField).ToList();
        target.SafeUpdate = ticket.SafeUpdate;
        target.UpdatedStamp = ticket.UpdatedStamp;
        if (ticket.Tags is not null)
            target.Tags = new TicketObject.TicketObject_tags { String = ticket.Tags.ToList() };
        if (ticket.Id is not null) target.AdditionalData["id"] = ticket.Id.Value;
    }

    /// <summary>Maps the curated comment write model onto the generated ticket comment object.</summary>
    private static TicketCommentObject? MapComment(ZendeskTicketCommentWrite? comment) =>
        comment is null
            ? null
            : new TicketCommentObject
            {
                Body = comment.Body,
                HtmlBody = comment.HtmlBody,
                Public = comment.Public,
                AuthorId = comment.AuthorId,
                Uploads = comment.Uploads?.ToList()
            };

    /// <summary>
    ///     Maps a curated custom-field value (<c>{ "id": ..., "value": ... }</c>). The value is free-typed
    ///     (string, number, bool, or array, matching the field type), so it is carried via <c>AdditionalData</c>
    ///     as raw JSON — the exact passthrough the old serializer performed.
    /// </summary>
    private static TicketFieldValueInput MapCustomField(ZendeskCustomFieldWrite field)
    {
        var input = new TicketFieldValueInput { Id = field.Id };
        if (field.Value is not null)
            input.AdditionalData["value"] = field.Value as JsonElement? ??
                                            JsonSerializer.SerializeToElement(field.Value, WriteJsonOptions);
        return input;
    }

    /// <summary>
    ///     Maps a status/priority/type string onto the generated enum. Unknown values are passed through
    ///     <c>AdditionalData</c> verbatim so Zendesk (not this tool) stays the validator — exactly like the old
    ///     client, which sent the caller's string as-is.
    /// </summary>
    private static TEnum? MapEnum<TEnum>(string? value, IDictionary<string, object> additionalData,
        string fieldName) where TEnum : struct, Enum
    {
        if (value is null) return null;
        if (Enum.TryParse<TEnum>(value, true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;
        additionalData[fieldName] = value;
        return null;
    }

    /// <summary>
    ///     Maps the curated ticket import model onto the generated <see cref="TicketImportInput" />. The committed
    ///     spec DOES model import <c>comments</c> as comment objects (an <c>allOf</c> of <c>{value}</c> +
    ///     <c>TicketCommentObject</c>), but <c>TicketCommentObject.created_at</c> is <c>readOnly</c>, so Kiota's
    ///     generated <c>Serialize</c> drops it — while the official docs explicitly allow a historical comment
    ///     <c>created_at</c> on import ("You can also set the comment's created_at time stamp. However, you can't
    ///     set the time stamp before 1970 or in the future.",
    ///     https://developer.zendesk.com/api-reference/ticketing/tickets/ticket_import/). The comments therefore
    ///     ride via <c>AdditionalData</c> as raw JSON in the documented shape (spec-anomaly ledger row in
    ///     <c>src/ES.FX.Zendesk/OpenApi/README.md</c>).
    /// </summary>
    private static TicketImportInput MapImport(ZendeskTicketImport ticket)
    {
        var input = new TicketImportInput
        {
            Subject = ticket.Subject,
            Description = ticket.Description,
            RequesterId = ticket.RequesterId,
            AssigneeId = ticket.AssigneeId,
            BrandId = ticket.BrandId,
            Tags = ticket.Tags?.ToList(),
            CustomFields = ticket.CustomFields?.Select(MapCustomField).ToList(),
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            SolvedAt = ticket.SolvedAt
        };
        input.Status = MapEnum<TicketImportInput_status>(ticket.Status, input.AdditionalData, "status");
        if (ticket.Comments is not null)
            input.AdditionalData["comments"] = JsonSerializer.SerializeToElement(ticket.Comments, WriteJsonOptions);
        return input;
    }
}