using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP write tools for Zendesk tickets (create/update/delete/merge/spam/restore/tags/comments/import).
///     Namespaced <c>tickets_*</c>; every tool routes through <see cref="ZendeskToolInvoker" /> so the
///     server execution mode (read-only / dry-run) is always honored.
/// </summary>
[McpServerToolType]
public sealed class ZendeskTicketWriteTools(IZendeskClient zendeskApiClient, IMcpExecutionModeAccessor executionMode)
{
    /// <summary>Creates a Zendesk ticket.</summary>
    [McpServerTool(Name = "tickets_create", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Creates a Zendesk ticket. The write model's 'comment' becomes the ticket description and is effectively " +
        "required; business rules (triggers/automations) and notifications fire. Attach files by putting upload " +
        "tokens from uploads_create in comment.uploads. For historical data (no triggers/notifications) use " +
        "tickets_import instead. Returns the created ticket. Write operation — honors the server execution " +
        "mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Create(
        [Description(
            "The ticket to create. Unset (null) fields are omitted. 'comment' becomes the description; set " +
            "comment.public=false only if the description should be an internal note. Allowed status: new, open, " +
            "pending, hold, solved, closed. Allowed priority: low, normal, high, urgent. Allowed type: problem, " +
            "incident, question, task.")]
        ZendeskTicketWrite ticket,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create a ticket with subject '{ticket.Subject}'",
            () => zendeskApiClient.Tickets.CreateAsync(ticket, cancellationToken), ticket);

    /// <summary>Creates up to 100 Zendesk tickets as an async job.</summary>
    [McpServerTool(Name = "tickets_create_many", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Creates up to 100 Zendesk tickets in one call as an async job. Returns a job_status — poll " +
        "job_statuses_get until completed; per-ticket outcomes (including partial failures) are in the " +
        "job's results. For historical/backfill data prefer tickets_import_many, which skips triggers and " +
        "notifications. Write operation — honors the server execution mode: rejected in read-only mode, simulated " +
        "(no changes made) in dry-run mode.")]
    public Task<object> CreateMany(
        [Description(
            "The tickets to create (1-100 per call). Same shape as tickets_create. Allowed status: new, open, " +
            "pending, hold, solved, closed. Allowed priority: low, normal, high, urgent. Allowed type: problem, " +
            "incident, question, task.")]
        ZendeskTicketWrite[] tickets,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"create {tickets.Length} tickets",
            () => zendeskApiClient.Tickets.CreateManyAsync(tickets, cancellationToken), tickets);

    /// <summary>Updates a Zendesk ticket by id.</summary>
    [McpServerTool(Name = "tickets_update", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Updates a Zendesk ticket by id — only the fields set on the write model are sent, everything else is left " +
        "untouched. To reply to the requester, set comment.body with comment.public=true; for an internal agent " +
        "note set comment.public=false (Zendesk defaults to public — always set the flag explicitly when adding a " +
        "comment). Concurrent updates fail with 409 Conflict; for explicit optimistic locking set safe_update=true " +
        "plus updated_stamp (the ticket's latest updated_at from tickets_get). Returns the updated ticket " +
        "and the audit of the change. Write operation — honors the server execution mode: rejected in read-only " +
        "mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Update(
        [Description("The numeric Zendesk ticket id.")]
        long id,
        [Description(
            "The changes to apply. Unset (null) fields are omitted. 'comment' appends a public reply " +
            "(public=true) or internal note (public=false); set safe_update+updated_stamp for optimistic locking. " +
            "Allowed status: new, open, pending, hold, solved, closed. Allowed priority: low, normal, high, urgent. " +
            "Allowed type: problem, incident, question, task.")]
        ZendeskTicketWrite ticket,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"update ticket {id}",
            () => zendeskApiClient.Tickets.UpdateAsync(id, ticket, cancellationToken), new { id, ticket });

    /// <summary>Applies the same change to up to 100 tickets as an async job.</summary>
    [McpServerTool(Name = "tickets_update_many", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Applies the SAME change to up to 100 tickets as an async job. For tag edits use the change's " +
        "additional_tags / remove_tags (not 'tags') — that is also the only way to change tags on closed tickets. " +
        "For different changes per ticket use tickets_update_many_batch. Returns a job_status — poll " +
        "job_statuses_get until completed. Write operation — honors the server execution mode: rejected " +
        "in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> UpdateMany(
        [Description("The numeric ids of the tickets to update (1-100 per call).")]
        long[] ids,
        [Description(
            "The single change applied to every ticket. Use additional_tags/remove_tags for tag edits; leave 'id' " +
            "unset. Allowed status: new, open, pending, hold, solved, closed. Allowed priority: low, normal, high, " +
            "urgent. Allowed type: problem, incident, question, task.")]
        ZendeskTicketWrite change,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"update {ids.Length} tickets with the same change",
            () => zendeskApiClient.Tickets.UpdateManyAsync(ids, change, cancellationToken), new { ids, change });

    /// <summary>Applies per-ticket changes to up to 100 tickets as an async job.</summary>
    [McpServerTool(Name = "tickets_update_many_batch", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "Applies PER-TICKET changes to up to 100 tickets as an async job — every item MUST carry its 'id'. For the " +
        "same change across many tickets prefer tickets_update_many. Returns a job_status — poll " +
        "job_statuses_get until completed. Write operation — honors the server execution mode: rejected " +
        "in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> UpdateManyBatch(
        [Description(
            "The per-ticket changes (1-100 per call); every item must have 'id' set. Allowed status: new, open, " +
            "pending, hold, solved, closed. Allowed priority: low, normal, high, urgent. Allowed type: problem, " +
            "incident, question, task.")]
        ZendeskTicketWrite[] tickets,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"update {tickets.Length} tickets with per-ticket changes",
            () => zendeskApiClient.Tickets.UpdateManyAsync(tickets, cancellationToken), tickets);

    /// <summary>Soft-deletes a Zendesk ticket.</summary>
    [McpServerTool(Name = "tickets_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Soft-deletes a ticket. Recoverable for ~30 days via tickets_restore; after that Zendesk purges it. " +
        "Rate-limited to 400 ticket deletions per minute. For irreversible removal see tickets_delete_permanently. " +
        "Returns a completion acknowledgement. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes " +
        "made) in dry-run mode.")]
    public Task<object> Delete(
        [Description("The numeric Zendesk ticket id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"soft-delete ticket {id}",
            () => zendeskApiClient.Tickets.DeleteAsync(id, cancellationToken), new { id });

    /// <summary>Soft-deletes up to 100 tickets as an async job.</summary>
    [McpServerTool(Name = "tickets_delete_many", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Soft-deletes up to 100 tickets as an async job. Recoverable for ~30 days via tickets_restore(_many). " +
        "Returns a job_status — poll job_statuses_get until completed. Write operation — honors the server " +
        "execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> DeleteMany(
        [Description("The numeric ids of the tickets to soft-delete (1-100 per call).")]
        long[] ids,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"soft-delete {ids.Length} tickets",
            () => zendeskApiClient.Tickets.DeleteManyAsync(ids, cancellationToken), new { ids });

    /// <summary>Merges source tickets into a target ticket as an async job.</summary>
    [McpServerTool(Name = "tickets_merge", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Merges source tickets into a target ticket as an async job — the sources are closed and their " +
        "conversations folded into the target; a merge cannot be undone. Merge comments default to private " +
        "(internal); set the *_comment_is_public flags to make them visible to requesters. Returns a job_status — " +
        "poll job_statuses_get until completed. Write operation — honors the server execution mode: " +
        "rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Merge(
        [Description("The numeric id of the ticket that survives (receives the merged conversations).")]
        long targetTicketId,
        [Description("The numeric ids of the tickets to merge into the target; they are closed by the merge.")]
        long[] sourceTicketIds,
        [Description("Comment placed on the target ticket describing the merge (optional).")]
        string? targetComment = null,
        [Description("Comment placed on each source ticket describing the merge (optional).")]
        string? sourceComment = null,
        [Description("Whether the target-ticket merge comment is public; Zendesk defaults to private (optional).")]
        bool? targetCommentIsPublic = null,
        [Description("Whether the source-ticket merge comment is public; Zendesk defaults to private (optional).")]
        bool? sourceCommentIsPublic = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"merge tickets: {string.Join(", ", sourceTicketIds)} (sources) into {targetTicketId} (target)",
            () => zendeskApiClient.Tickets.MergeAsync(targetTicketId, sourceTicketIds,
                targetComment: targetComment, sourceComment: sourceComment,
                targetCommentIsPublic: targetCommentIsPublic, sourceCommentIsPublic: sourceCommentIsPublic,
                cancellationToken: cancellationToken),
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
        OpenWorld = true)]
    [Description(
        "Marks a ticket as spam AND suspends its requester — the requester can no longer submit tickets until " +
        "unsuspended, so verify the sender really is a spammer first. Returns a completion acknowledgement. Write " +
        "operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in " +
        "dry-run mode.")]
    public Task<object> MarkSpam(
        [Description("The numeric Zendesk ticket id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"mark ticket {id} as spam and suspend its requester",
            () => zendeskApiClient.Tickets.MarkAsSpamAsync(id, cancellationToken), new { id });

    /// <summary>Marks up to 100 tickets as spam as an async job.</summary>
    [McpServerTool(Name = "tickets_mark_spam_many", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Marks up to 100 tickets as spam as an async job, suspending their requesters. Returns a job_status — poll " +
        "job_statuses_get until completed. Write operation — honors the server execution mode: rejected " +
        "in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> MarkSpamMany(
        [Description("The numeric ids of the tickets to mark as spam (1-100 per call).")]
        long[] ids,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"mark {ids.Length} tickets as spam",
            () => zendeskApiClient.Tickets.MarkManyAsSpamAsync(ids, cancellationToken), new { ids });

    /// <summary>Restores a soft-deleted ticket.</summary>
    [McpServerTool(Name = "tickets_restore", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Restores a soft-deleted ticket (undoes tickets_delete within the ~30-day recovery window). " +
        "Returns a completion acknowledgement. Write operation — honors the server execution mode: rejected in " +
        "read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Restore(
        [Description("The numeric id of the soft-deleted ticket.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"restore soft-deleted ticket {id}",
            () => zendeskApiClient.Tickets.RestoreDeletedAsync(id, cancellationToken), new { id });

    /// <summary>Restores up to 100 soft-deleted tickets.</summary>
    [McpServerTool(Name = "tickets_restore_many", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Restores up to 100 soft-deleted tickets in one call. Unlike most bulk ticket operations this endpoint is " +
        "synchronous — no job is created. Returns a completion acknowledgement. Write operation — honors the " +
        "server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> RestoreMany(
        [Description("The numeric ids of the soft-deleted tickets to restore (1-100 per call).")]
        long[] ids,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"restore {ids.Length} soft-deleted tickets",
            () => zendeskApiClient.Tickets.RestoreManyDeletedAsync(ids, cancellationToken), new { ids });

    /// <summary>Permanently deletes an already soft-deleted ticket (irreversible).</summary>
    [McpServerTool(Name = "tickets_delete_permanently", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = true)]
    [Description(
        "PERMANENTLY deletes a ticket that was already soft-deleted (tickets_delete first). IRREVERSIBLE — " +
        "the ticket cannot be recovered afterwards. Async even for a single ticket: returns a job_status — poll " +
        "job_statuses_get until completed. Write operation — honors the server execution mode: rejected " +
        "in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> DeletePermanently(
        [Description("The numeric id of the already soft-deleted ticket.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"PERMANENTLY delete ticket {id} (irreversible)",
            () => zendeskApiClient.Tickets.DeletePermanentlyAsync(id, cancellationToken), new { id });

    /// <summary>Permanently deletes up to 100 soft-deleted tickets as an async job (irreversible).</summary>
    [McpServerTool(Name = "tickets_delete_permanently_many", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "PERMANENTLY deletes up to 100 already soft-deleted tickets as an async job. IRREVERSIBLE — the tickets " +
        "cannot be recovered afterwards. Returns a job_status — poll job_statuses_get until completed. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes " +
        "made) in dry-run mode.")]
    public Task<object> DeletePermanentlyMany(
        [Description("The numeric ids of the already soft-deleted tickets (1-100 per call).")]
        long[] ids,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"PERMANENTLY delete {ids.Length} tickets (irreversible)",
            () => zendeskApiClient.Tickets.DeleteManyPermanentlyAsync(ids, cancellationToken), new { ids });

    /// <summary>Replaces a ticket's whole tag set.</summary>
    [McpServerTool(Name = "tickets_tags_set", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "REPLACES a ticket's whole tag set with the given tags — any tag not in the list is removed. To add or " +
        "remove specific tags without touching the rest, use tickets_tags_add / tickets_tags_remove. " +
        "Does not work on closed tickets (use additional_tags/remove_tags via tickets_update_many for " +
        "those). Returns the ticket's resulting tag list. Write operation — honors the server execution mode: " +
        "rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> TagsSet(
        [Description("The numeric Zendesk ticket id.")]
        long ticketId,
        [Description("The complete new tag set; existing tags not listed here are removed.")]
        string[] tags,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"replace the tag set of ticket {ticketId} with [{string.Join(", ", tags)}]",
            () => zendeskApiClient.Tickets.SetTagsAsync(ticketId, tags, cancellationToken),
            new { ticketId, tags });

    /// <summary>Adds tags to a ticket without removing existing ones.</summary>
    [McpServerTool(Name = "tickets_tags_add", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Adds tags to a ticket without touching its existing tags. Optionally pass updatedStamp (the ticket's " +
        "latest updated_at from tickets_get) for optimistic-concurrency protection: if the ticket changed " +
        "in the meantime, Zendesk rejects the call with 409 Conflict instead of silently overwriting — re-read and " +
        "retry. Does not work on closed tickets. Returns the ticket's resulting tag list. Write operation — honors " +
        "the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> TagsAdd(
        [Description("The numeric Zendesk ticket id.")]
        long ticketId,
        [Description("The tags to add.")]
        string[] tags,
        [Description(
            "The ticket's latest updated_at, for safe-update collision protection (409 on mismatch). Optional.")]
        DateTimeOffset? updatedStamp = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"add tags [{string.Join(", ", tags)}] to ticket {ticketId}",
            () => zendeskApiClient.Tickets.AddTagsAsync(ticketId, tags, updatedStamp: updatedStamp,
                cancellationToken: cancellationToken),
            new { ticketId, tags, updatedStamp });

    /// <summary>Removes tags from a ticket.</summary>
    [McpServerTool(Name = "tickets_tags_remove", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Removes the given tags from a ticket, leaving other tags in place. Optionally pass updatedStamp (the " +
        "ticket's latest updated_at from tickets_get) for optimistic-concurrency protection: a concurrent " +
        "change makes the call fail with 409 Conflict instead of silently overwriting — re-read and retry. Does " +
        "not work on closed tickets. Returns the ticket's resulting tag list. Write operation — honors the server " +
        "execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> TagsRemove(
        [Description("The numeric Zendesk ticket id.")]
        long ticketId,
        [Description("The tags to remove.")]
        string[] tags,
        [Description(
            "The ticket's latest updated_at, for safe-update collision protection (409 on mismatch). Optional.")]
        DateTimeOffset? updatedStamp = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"remove tags [{string.Join(", ", tags)}] from ticket {ticketId}",
            () => zendeskApiClient.Tickets.RemoveTagsAsync(ticketId, tags, updatedStamp: updatedStamp,
                cancellationToken: cancellationToken),
            new { ticketId, tags, updatedStamp });

    /// <summary>Makes a public ticket comment private.</summary>
    [McpServerTool(Name = "tickets_comments_make_private", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = true)]
    [Description(
        "Makes a public ticket comment private (an internal note). ONE-WAY: Zendesk has no make-public — the only " +
        "way back is deleting and re-adding the content as a new public comment. Find comment ids with " +
        "tickets_comments_list. Returns a completion acknowledgement. Write operation — honors the server " +
        "execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> CommentMakePrivate(
        [Description("The numeric Zendesk ticket id.")]
        long ticketId,
        [Description("The numeric id of the comment to make private.")]
        long commentId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"make comment {commentId} on ticket {ticketId} private",
            () => zendeskApiClient.Tickets.MakeCommentPrivateAsync(ticketId, commentId, cancellationToken),
            new { ticketId, commentId });

    /// <summary>Permanently redacts a comment attachment (irreversible).</summary>
    [McpServerTool(Name = "tickets_comments_attachment_redact", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = true)]
    [Description(
        "PERMANENTLY redacts an attachment on a ticket comment, replacing the file with an empty 'redacted.txt'. " +
        "IRREVERSIBLE — the original file cannot be recovered — and not possible once the ticket is closed. Find " +
        "comment and attachment ids with tickets_comments_list. Returns the redacted attachment. Write " +
        "operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in " +
        "dry-run mode.")]
    public Task<object> CommentAttachmentRedact(
        [Description("The numeric Zendesk ticket id.")]
        long ticketId,
        [Description("The numeric id of the comment carrying the attachment.")]
        long commentId,
        [Description("The numeric id of the attachment to redact.")]
        long attachmentId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"permanently redact attachment {attachmentId} on comment {commentId} of ticket {ticketId} (irreversible)",
            () => zendeskApiClient.Tickets.RedactCommentAttachmentAsync(ticketId, commentId, attachmentId,
                cancellationToken),
            new { ticketId, commentId, attachmentId });

    /// <summary>Imports a historical ticket (admin-only; no triggers/notifications).</summary>
    [McpServerTool(Name = "tickets_import", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Imports a HISTORICAL ticket (admin-only) — unlike tickets_create it accepts a whole comment " +
        "conversation and historical created_at/updated_at/solved_at timestamps, and it skips triggers, " +
        "notifications, metrics, and SLAs. Set archiveImmediately=true to send closed tickets straight to the " +
        "archive (recommended for backfills). Returns the imported ticket. Write operation — honors the server " +
        "execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Import(
        [Description(
            "The historical ticket to import, including its comments (each with author_id, body, public, " +
            "created_at) and historical timestamps.")]
        ZendeskTicketImport ticket,
        [Description("Send the ticket directly to the archive after import (only valid for closed tickets).")]
        bool archiveImmediately = false,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"import a historical ticket with subject '{ticket.Subject}'",
            () => zendeskApiClient.Tickets.ImportAsync(ticket, archiveImmediately: archiveImmediately,
                cancellationToken: cancellationToken),
            new { ticket, archiveImmediately });

    /// <summary>Imports up to 100 historical tickets as an async job (admin-only).</summary>
    [McpServerTool(Name = "tickets_import_many", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Imports up to 100 HISTORICAL tickets as an async job (admin-only) — the bulk form of " +
        "tickets_import: whole comment conversations and historical timestamps are accepted; triggers, " +
        "notifications, metrics, and SLAs are skipped. Set archiveImmediately=true to send closed tickets straight " +
        "to the archive. Returns a job_status — poll job_statuses_get until completed. Write operation — " +
        "honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> ImportMany(
        [Description("The historical tickets to import (1-100 per call). Same shape as tickets_import.")]
        ZendeskTicketImport[] tickets,
        [Description("Send the tickets directly to the archive after import (only valid for closed tickets).")]
        bool archiveImmediately = false,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"import {tickets.Length} historical tickets",
            () => zendeskApiClient.Tickets.ImportManyAsync(tickets, archiveImmediately: archiveImmediately,
                cancellationToken: cancellationToken),
            new { tickets, archiveImmediately });
}
