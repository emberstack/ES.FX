using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk <c>tickets</c> resource.
/// </summary>
public interface IZendeskTicketsApi
{
    /// <summary>Returns a ticket by id (<c>GET /api/v2/tickets/{id}.json</c>).</summary>
    Task<ZendeskTicket> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Searches tickets via the Zendesk search API (<c>GET /api/v2/search.json</c>). The query is automatically
    ///     scoped to <c>type:ticket</c> unless it already contains a <c>type:</c> selector.
    /// </summary>
    /// <param name="query">The Zendesk search query (e.g. <c>status:open priority:high</c>).</param>
    /// <param name="sortBy">The sort field — see <see cref="ZendeskTicketSortFields" />.</param>
    /// <param name="sortOrder"><c>asc</c> or <c>desc</c> — see <see cref="ZendeskSortOrders" />.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="perPage">The number of results per page (Zendesk caps at 100).</param>
    /// <param name="include">
    ///     Sideloads to resolve inline in one call (any of <c>users</c>, <c>groups</c>, <c>organizations</c>);
    ///     returned as sibling arrays on the result.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<ZendeskTicketSearchResults> SearchAsync(string query, string? sortBy = null, string? sortOrder = null,
        int? page = null, int? perPage = null, IReadOnlyList<string>? include = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a ticket's comments — the conversation thread (<c>GET /api/v2/tickets/{id}/comments.json</c>).
    /// </summary>
    /// <param name="ticketId">The ticket id.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="perPage">The number of results per page.</param>
    /// <param name="bodyFormat">
    ///     Which comment body to return — see <see cref="ZendeskCommentBodyFormats" />: <c>plain</c> (default —
    ///     plain text only, half the tokens), <c>rich</c> (markup only), or <c>both</c>. Unrecognized values are
    ///     treated as <c>plain</c>.
    /// </param>
    /// <param name="include">
    ///     Sideloads — <see cref="ZendeskSideloads.Users" /> resolves comment authors inline as a sibling array
    ///     in the same roundtrip.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<ZendeskTicketCommentsResult> GetCommentsAsync(long ticketId, int? page = null, int? perPage = null,
        string? bodyFormat = ZendeskCommentBodyFormats.Plain, IReadOnlyList<string>? include = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a ticket's audits — its change history/events (<c>GET /api/v2/tickets/{id}/audits.json</c>).
    ///     <paramref name="include" /> sideloads (<c>users</c>, <c>groups</c>, <c>organizations</c>) resolve the
    ///     actors referenced by the events in the same roundtrip.
    /// </summary>
    Task<ZendeskTicketAuditsResult> GetAuditsAsync(long ticketId, int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a ticket's timing/lifecycle metrics (<c>GET /api/v2/tickets/{id}/metrics.json</c>).
    /// </summary>
    Task<ZendeskTicketMetric> GetMetricsAsync(long ticketId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the incident tickets linked to a <c>problem</c> ticket
    ///     (<c>GET /api/v2/tickets/{id}/incidents.json</c>) — the blast radius of a systemic problem.
    /// </summary>
    Task<ZendeskTicketsResult> GetIncidentsAsync(long problemTicketId, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a ticket's side conversations — separate vendor/escalation threads off the main comment thread
    ///     (<c>GET /api/v2/tickets/{id}/side_conversations.json</c>; requires the Collaboration add-on).
    /// </summary>
    Task<ZendeskSideConversationsResult> GetSideConversationsAsync(long ticketId, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Exports ticket metric events — timestamped SLA/metric lifecycle events (<c>apply_sla</c>,
    ///     <c>breach</c>, <c>activate</c>, <c>fulfill</c>, ...) across ALL tickets, starting at
    ///     <paramref name="startTime" /> (<c>GET /api/v2/incremental/ticket_metric_events.json?start_time=...</c> —
    ///     the only metric-events endpoint Zendesk provides; there is no per-ticket variant). Filter the returned
    ///     events by <c>ticket_id</c> for a single ticket. Continue paging by passing the response's
    ///     <see cref="ZendeskMetricEventsResult.EndTime" /> as the next <paramref name="startTime" /> until
    ///     <see cref="ZendeskMetricEventsResult.EndOfStream" /> is <c>true</c>. Subject to Zendesk's incremental
    ///     export rate limits.
    /// </summary>
    /// <param name="startTime">Unix epoch seconds; events recorded at or after this time are returned.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<ZendeskMetricEventsResult> GetMetricEventsAsync(long startTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists tickets (<c>GET /api/v2/tickets.json</c>; cursor-paginated — page with
    ///     <c>Meta.AfterCursor</c>). Archived tickets are excluded; the endpoint has its own per-account rate
    ///     limit. <paramref name="include" /> sideloads resolve related records inline.
    /// </summary>
    Task<ZendeskTicketsResult> ListAsync(int? pageSize = null, string? afterCursor = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns many tickets by id in a single request (<c>GET /api/v2/tickets/show_many.json?ids=</c>).
    ///     Zendesk accepts up to 100 ids per request; larger lists are chunked and merged (sideload arrays are
    ///     merged and de-duplicated by id). An empty list returns an empty result without a call.
    ///     <paramref name="include" /> sideloads (<c>users</c>, <c>groups</c>, <c>organizations</c>,
    ///     <c>comment_count</c>) resolve related records in the same roundtrip.
    /// </summary>
    Task<ZendeskTicketsResult> GetManyAsync(IReadOnlyList<long> ids, IReadOnlyList<string>? include = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the (cached, approximate) account ticket count (<c>GET /api/v2/tickets/count.json</c>).
    /// </summary>
    Task<ZendeskCount> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the tickets carrying an external id (<c>GET /api/v2/tickets.json?external_id=</c>) — the
    ///     link between Zendesk tickets and records in an outside system.
    /// </summary>
    Task<ZendeskTicketsResult> GetByExternalIdAsync(string externalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists the collaborators (CCs) of a ticket (<c>GET /api/v2/tickets/{id}/collaborators.json</c>;
    ///     <c>users</c> envelope).
    /// </summary>
    Task<ZendeskUsersResult> GetCollaboratorsAsync(long ticketId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the (cached, approximate) comment count of a ticket
    ///     (<c>GET /api/v2/tickets/{id}/comments/count.json</c>) — cheaper than paging the thread to size it.
    /// </summary>
    Task<ZendeskCount> GetCommentsCountAsync(long ticketId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Exports tickets incrementally via the cursor-based incremental export
    ///     (<c>GET /api/v2/incremental/tickets/cursor.json</c>) — the recommended way to sync full ticket
    ///     history, including archived tickets that list endpoints omit. Start with
    ///     <paramref name="startTime" />; continue by passing the response's
    ///     <see cref="ZendeskIncrementalTicketsResult.AfterCursor" /> as <paramref name="cursor" /> until
    ///     <see cref="ZendeskIncrementalTicketsResult.EndOfStream" /> is <c>true</c>. Admin-only; subject to the
    ///     incremental-export rate limit.
    /// </summary>
    /// <param name="startTime">Unix epoch seconds for the initial call (exactly one of the two parameters).</param>
    /// <param name="cursor">The continuation cursor from the previous page (exactly one of the two parameters).</param>
    /// <param name="include">
    ///     Sideloads (<c>users</c>, <c>groups</c>, <c>organizations</c>) resolved inline; <c>last_audits</c> is
    ///     NOT supported on the incremental export.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<ZendeskIncrementalTicketsResult> GetIncrementalAsync(long? startTime = null, string? cursor = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a ticket (<c>POST /api/v2/tickets.json</c>). The <c>Comment</c> becomes the ticket description
    ///     and is effectively required. Business rules and notifications fire.
    /// </summary>
    Task<ZendeskTicket> CreateAsync(ZendeskTicketWrite ticket, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates up to 100 tickets as an async job (<c>POST /api/v2/tickets/create_many.json</c>). Poll the
    ///     returned job via <see cref="IZendeskJobStatusesApi" />. For historical data prefer
    ///     <see cref="ImportManyAsync" />.
    /// </summary>
    Task<ZendeskJobStatus> CreateManyAsync(IReadOnlyList<ZendeskTicketWrite> tickets,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates a ticket (<c>PUT /api/v2/tickets/{id}.json</c>). Returns both the updated ticket and the audit
    ///     of the change. Since 2025-05-15 concurrent updates fail with <c>409 Conflict</c>; set
    ///     <c>SafeUpdate</c> + <c>UpdatedStamp</c> for explicit collision protection.
    /// </summary>
    Task<ZendeskTicketUpdateResult> UpdateAsync(long id, ZendeskTicketWrite ticket,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Applies the SAME change to up to 100 tickets as an async job
    ///     (<c>PUT /api/v2/tickets/update_many.json?ids=</c>). Use <c>AdditionalTags</c>/<c>RemoveTags</c> for tag
    ///     edits (also the only way to change tags on closed tickets).
    /// </summary>
    Task<ZendeskJobStatus> UpdateManyAsync(IReadOnlyList<long> ids, ZendeskTicketWrite change,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Applies PER-TICKET changes to up to 100 tickets as an async job
    ///     (<c>PUT /api/v2/tickets/update_many.json</c>, batch form). Every item must carry <c>Id</c>.
    /// </summary>
    Task<ZendeskJobStatus> UpdateManyAsync(IReadOnlyList<ZendeskTicketWrite> tickets,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a ticket (<c>DELETE /api/v2/tickets/{id}.json</c>; recoverable for ~30 days).</summary>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes up to 100 tickets as an async job (<c>DELETE /api/v2/tickets/destroy_many.json</c>).</summary>
    Task<ZendeskJobStatus> DeleteManyAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Merges source tickets into a target ticket as an async job
    ///     (<c>POST /api/v2/tickets/{targetId}/merge.json</c>). Merge comments default to private.
    /// </summary>
    Task<ZendeskJobStatus> MergeAsync(long targetTicketId, IReadOnlyList<long> sourceTicketIds,
        string? targetComment = null, string? sourceComment = null, bool? targetCommentIsPublic = null,
        bool? sourceCommentIsPublic = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Marks a ticket as spam and suspends its requester
    ///     (<c>PUT /api/v2/tickets/{id}/mark_as_spam.json</c>).
    /// </summary>
    Task MarkAsSpamAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Marks up to 100 tickets as spam as an async job (<c>PUT /api/v2/tickets/mark_many_as_spam.json</c>).</summary>
    Task<ZendeskJobStatus> MarkManyAsSpamAsync(IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Restores a soft-deleted ticket (<c>PUT /api/v2/deleted_tickets/{id}/restore.json</c>).
    /// </summary>
    Task RestoreDeletedAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Restores up to 100 soft-deleted tickets (<c>PUT /api/v2/deleted_tickets/restore_many.json</c>; synchronous).</summary>
    Task RestoreManyDeletedAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default);

    /// <summary>
    ///     PERMANENTLY deletes an already soft-deleted ticket (<c>DELETE /api/v2/deleted_tickets/{id}.json</c>).
    ///     Irreversible; async even for a single ticket — poll the returned job.
    /// </summary>
    Task<ZendeskJobStatus> DeletePermanentlyAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     PERMANENTLY deletes up to 100 soft-deleted tickets as an async job
    ///     (<c>DELETE /api/v2/deleted_tickets/destroy_many.json</c>). Irreversible.
    /// </summary>
    Task<ZendeskJobStatus> DeleteManyPermanentlyAsync(IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     REPLACES a ticket's whole tag set (<c>POST /api/v2/tickets/{id}/tags.json</c>). Does not work on
    ///     closed tickets.
    /// </summary>
    Task<ZendeskTagNamesResult> SetTagsAsync(long ticketId, IReadOnlyList<string> tags,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds tags to a ticket (<c>PUT /api/v2/tickets/{id}/tags.json</c>). Pass
    ///     <paramref name="updatedStamp" /> (the ticket's latest <c>updated_at</c>) for safe-update collision
    ///     protection (<c>409</c> on mismatch). Does not work on closed tickets.
    /// </summary>
    Task<ZendeskTagNamesResult> AddTagsAsync(long ticketId, IReadOnlyList<string> tags,
        DateTimeOffset? updatedStamp = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes tags from a ticket (<c>DELETE /api/v2/tickets/{id}/tags.json</c>, body form). Supports the
    ///     same safe-update protection as <see cref="AddTagsAsync" />. Does not work on closed tickets.
    /// </summary>
    Task<ZendeskTagNamesResult> RemoveTagsAsync(long ticketId, IReadOnlyList<string> tags,
        DateTimeOffset? updatedStamp = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Makes a public comment private (<c>PUT .../comments/{id}/make_private.json</c>). One-way — there is no
    ///     make-public.
    /// </summary>
    Task MakeCommentPrivateAsync(long ticketId, long commentId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     PERMANENTLY redacts a comment attachment, replacing the file with an empty <c>redacted.txt</c>
    ///     (<c>PUT .../comments/{commentId}/attachments/{attachmentId}/redact.json</c>). Irreversible; not
    ///     possible once the ticket is closed.
    /// </summary>
    Task<ZendeskAttachment> RedactCommentAttachmentAsync(long ticketId, long commentId, long attachmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Imports a historical ticket (<c>POST /api/v2/imports/tickets.json</c>; admin-only). Accepts a whole
    ///     comment conversation and historical timestamps; triggers/metrics/SLAs are not applied.
    /// </summary>
    Task<ZendeskTicket> ImportAsync(ZendeskTicketImport ticket, bool archiveImmediately = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Imports up to 100 historical tickets as an async job
    ///     (<c>POST /api/v2/imports/tickets/create_many.json</c>; admin-only).
    /// </summary>
    Task<ZendeskJobStatus> ImportManyAsync(IReadOnlyList<ZendeskTicketImport> tickets,
        bool archiveImmediately = false, CancellationToken cancellationToken = default);
}