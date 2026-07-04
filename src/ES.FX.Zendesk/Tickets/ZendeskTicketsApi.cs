using System.Globalization;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.Tickets;

/// <summary>
///     Default <see cref="IZendeskTicketsApi" /> implementation over the shared Zendesk <see cref="HttpClient" />.
/// </summary>
internal sealed partial class ZendeskTicketsApi(HttpClient httpClient, ILogger<ZendeskTicketsApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskTicketsApi
{
    /// <summary>Zendesk's documented maximum for <c>show_many</c>; larger lists are rejected with 400.</summary>
    private const int MaxIdsPerShowManyRequest = 100;

    /// <inheritdoc />
    public async Task<ZendeskTicket> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskTicketResponse>($"tickets/{id}.json", "Zendesk.Tickets.Get",
            cancellationToken).ConfigureAwait(false);
        return response.Ticket ?? throw new InvalidOperationException($"Zendesk ticket '{id}' was not found.");
    }

    /// <inheritdoc />
    public Task<ZendeskTicketSearchResults> SearchAsync(string query, string? sortBy = null, string? sortOrder = null,
        int? page = null, int? perPage = null, IReadOnlyList<string>? include = null,
        CancellationToken cancellationToken = default)
    {
        // Scope the search to tickets unless the caller already supplied a `type:` result-type selector.
        // Match `type:` only at a token boundary so unrelated qualifiers (ticket_type:, support_type:,
        // content-type:, or free text) do not disable scoping and leave the search running across all record types.
        var scopedQuery = TypeSelectorRegex().IsMatch(query) ? query : $"type:ticket {query}".Trim();

        // The Search API sideloads with the nested `include=tickets(users,organizations)` syntax (unlike list
        // endpoints, which use a flat list).
        var sideload = ZendeskQuery.Include(include);
        var scopedInclude = sideload is null ? null : $"tickets({sideload})";

        var requestUri = ZendeskQuery.Build("search.json",
            ("query", scopedQuery), ("sort_by", sortBy), ("sort_order", sortOrder),
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)), ("include", scopedInclude));
        return GetAsync<ZendeskTicketSearchResults>(requestUri, "Zendesk.Tickets.Search", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskTicketCommentsResult> GetCommentsAsync(long ticketId, int? page = null,
        int? perPage = null, string? bodyFormat = ZendeskCommentBodyFormats.Plain,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"tickets/{ticketId}/comments.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)),
            ("include", ZendeskQuery.Include(include)));
        var result = await GetAsync<ZendeskTicketCommentsResult>(requestUri, "Zendesk.Tickets.Comments",
            cancellationToken).ConfigureAwait(false);
        return ProjectBodies(result, bodyFormat);
    }

    /// <inheritdoc />
    public Task<ZendeskTicketAuditsResult> GetAuditsAsync(long ticketId, int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"tickets/{ticketId}/audits.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)),
            ("include", ZendeskQuery.Include(include)));
        return GetAsync<ZendeskTicketAuditsResult>(requestUri, "Zendesk.Tickets.Audits", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskTicketMetric> GetMetricsAsync(long ticketId, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskTicketMetricResponse>($"tickets/{ticketId}/metrics.json",
            "Zendesk.Tickets.Metrics", cancellationToken).ConfigureAwait(false);
        return response.TicketMetric
               ?? throw new InvalidOperationException($"Zendesk returned no metrics for ticket '{ticketId}'.");
    }

    /// <inheritdoc />
    public Task<ZendeskTicketsResult> GetIncidentsAsync(long problemTicketId, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"tickets/{problemTicketId}/incidents.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskTicketsResult>(requestUri, "Zendesk.Tickets.Incidents", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskSideConversationsResult> GetSideConversationsAsync(long ticketId, int? page = null,
        int? perPage = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"tickets/{ticketId}/side_conversations.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskSideConversationsResult>(requestUri, "Zendesk.Tickets.SideConversations",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskMetricEventsResult> GetMetricEventsAsync(long startTime,
        CancellationToken cancellationToken = default)
    {
        // The incremental export is the ONLY metric-events endpoint Zendesk provides. A per-ticket
        // tickets/{id}/metric_events path does not exist (live-verified: it answers 200 with an empty body).
        var requestUri = ZendeskQuery.Build("incremental/ticket_metric_events.json",
            ("start_time", startTime.ToString(CultureInfo.InvariantCulture)));
        return GetAsync<ZendeskMetricEventsResult>(requestUri, "Zendesk.Tickets.MetricEvents", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskTicketsResult> ListAsync(int? pageSize = null, string? afterCursor = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("tickets.json",
            ("page[size]", ZendeskQuery.Int(pageSize)), ("page[after]", afterCursor),
            ("include", ZendeskQuery.Include(include)));
        return GetAsync<ZendeskTicketsResult>(requestUri, "Zendesk.Tickets.List", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskTicketsResult> GetManyAsync(IReadOnlyList<long> ids,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return new ZendeskTicketsResult();

        if (ids.Count <= MaxIdsPerShowManyRequest)
        {
            var requestUri = ZendeskQuery.Build("tickets/show_many.json",
                ("ids", string.Join(',', ids)), ("include", ZendeskQuery.Include(include)));
            return await GetAsync<ZendeskTicketsResult>(requestUri, "Zendesk.Tickets.GetMany", cancellationToken)
                .ConfigureAwait(false);
        }

        // show_many rejects more than 100 ids with 400 Bad Request — chunk and merge instead of failing the
        // batch. Sideload arrays are merged across chunks and de-duplicated by id.
        var tickets = new List<ZendeskTicket>(ids.Count);
        List<ZendeskUser>? users = null;
        List<ZendeskGroup>? groups = null;
        List<ZendeskOrganization>? organizations = null;
        for (var offset = 0; offset < ids.Count; offset += MaxIdsPerShowManyRequest)
        {
            var chunk = ids.Skip(offset).Take(MaxIdsPerShowManyRequest);
            var requestUri = ZendeskQuery.Build("tickets/show_many.json",
                ("ids", string.Join(',', chunk)), ("include", ZendeskQuery.Include(include)));
            var page = await GetAsync<ZendeskTicketsResult>(requestUri, "Zendesk.Tickets.GetMany", cancellationToken)
                .ConfigureAwait(false);
            tickets.AddRange(page.Tickets);
            if (page.Users is not null) (users ??= []).AddRange(page.Users);
            if (page.Groups is not null) (groups ??= []).AddRange(page.Groups);
            if (page.Organizations is not null) (organizations ??= []).AddRange(page.Organizations);
        }

        return new ZendeskTicketsResult
        {
            Tickets = tickets,
            Count = tickets.Count,
            Users = users?.DistinctBy(u => u.Id).ToList(),
            Groups = groups?.DistinctBy(g => g.Id).ToList(),
            Organizations = organizations?.DistinctBy(o => o.Id).ToList()
        };
    }

    /// <inheritdoc />
    public async Task<ZendeskCount> CountAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskCountResponse>("tickets/count.json", "Zendesk.Tickets.Count",
            cancellationToken).ConfigureAwait(false);
        return response.Count ?? throw new InvalidOperationException("Zendesk returned no ticket count.");
    }

    /// <inheritdoc />
    public Task<ZendeskTicketsResult> GetByExternalIdAsync(string externalId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        var requestUri = ZendeskQuery.Build("tickets.json", ("external_id", externalId));
        return GetAsync<ZendeskTicketsResult>(requestUri, "Zendesk.Tickets.GetByExternalId", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskUsersResult> GetCollaboratorsAsync(long ticketId,
        CancellationToken cancellationToken = default) =>
        GetAsync<ZendeskUsersResult>($"tickets/{ticketId}/collaborators.json", "Zendesk.Tickets.Collaborators",
            cancellationToken);

    /// <inheritdoc />
    public async Task<ZendeskCount> GetCommentsCountAsync(long ticketId,
        CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskCountResponse>($"tickets/{ticketId}/comments/count.json",
            "Zendesk.Tickets.CommentsCount", cancellationToken).ConfigureAwait(false);
        return response.Count
               ?? throw new InvalidOperationException($"Zendesk returned no comment count for ticket '{ticketId}'.");
    }

    /// <inheritdoc />
    public Task<ZendeskIncrementalTicketsResult> GetIncrementalAsync(long? startTime = null, string? cursor = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default)
    {
        // Exactly one of the two: start_time begins the export, cursor continues it.
        if (startTime is null == cursor is null)
            throw new ArgumentException(
                "Provide exactly one of startTime (initial call) or cursor (continuation).", nameof(startTime));

        var requestUri = ZendeskQuery.Build("incremental/tickets/cursor.json",
            ("start_time", startTime?.ToString(CultureInfo.InvariantCulture)), ("cursor", cursor),
            ("include", ZendeskQuery.Include(include)));
        return GetAsync<ZendeskIncrementalTicketsResult>(requestUri, "Zendesk.Tickets.Incremental",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskTicket> CreateAsync(ZendeskTicketWrite ticket,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<ZendeskTicketResponse>("tickets.json", new { ticket },
            "Zendesk.Tickets.Create", cancellationToken).ConfigureAwait(false);
        return response.Ticket ?? throw new InvalidOperationException("Zendesk returned no created ticket.");
    }

    /// <inheritdoc />
    public Task<ZendeskJobStatus> CreateManyAsync(IReadOnlyList<ZendeskTicketWrite> tickets,
        CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(tickets.Count, nameof(tickets));
        return SendJobAsync(HttpMethod.Post, "tickets/create_many.json", new { tickets },
            "Zendesk.Tickets.CreateMany", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskTicketUpdateResult> UpdateAsync(long id, ZendeskTicketWrite ticket,
        CancellationToken cancellationToken = default) =>
        PutAsync<ZendeskTicketUpdateResult>($"tickets/{id}.json", new { ticket }, "Zendesk.Tickets.Update",
            cancellationToken);

    /// <inheritdoc />
    public Task<ZendeskJobStatus> UpdateManyAsync(IReadOnlyList<long> ids, ZendeskTicketWrite change,
        CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(ids.Count, nameof(ids));
        var requestUri = ZendeskQuery.Build("tickets/update_many.json", ("ids", string.Join(',', ids)));
        return SendJobAsync(HttpMethod.Put, requestUri, new { ticket = change }, "Zendesk.Tickets.UpdateMany",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskJobStatus> UpdateManyAsync(IReadOnlyList<ZendeskTicketWrite> tickets,
        CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(tickets.Count, nameof(tickets));
        if (tickets.Any(t => t.Id is null))
            throw new ArgumentException("Every batch update item must carry Id.", nameof(tickets));
        return SendJobAsync(HttpMethod.Put, "tickets/update_many.json", new { tickets },
            "Zendesk.Tickets.UpdateManyBatch", cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"tickets/{id}.json", null, "Zendesk.Tickets.Delete", cancellationToken);

    /// <inheritdoc />
    public Task<ZendeskJobStatus> DeleteManyAsync(IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(ids.Count, nameof(ids));
        var requestUri = ZendeskQuery.Build("tickets/destroy_many.json", ("ids", string.Join(',', ids)));
        return SendJobAsync(HttpMethod.Delete, requestUri, null, "Zendesk.Tickets.DeleteMany", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskJobStatus> MergeAsync(long targetTicketId, IReadOnlyList<long> sourceTicketIds,
        string? targetComment = null, string? sourceComment = null, bool? targetCommentIsPublic = null,
        bool? sourceCommentIsPublic = null, CancellationToken cancellationToken = default)
    {
        // Merge has NO documented id cap (unlike the 100-item bulk endpoints) — only reject an empty list.
        if (sourceTicketIds.Count == 0)
            throw new ArgumentException("At least one source ticket id is required.", nameof(sourceTicketIds));
        // QUIRK: the merge payload is a BARE object — no "ticket" envelope.
        var payload = new
        {
            ids = sourceTicketIds,
            target_comment = targetComment,
            source_comment = sourceComment,
            target_comment_is_public = targetCommentIsPublic,
            source_comment_is_public = sourceCommentIsPublic
        };
        return SendJobAsync(HttpMethod.Post, $"tickets/{targetTicketId}/merge.json", payload,
            "Zendesk.Tickets.Merge", cancellationToken);
    }

    /// <inheritdoc />
    public Task MarkAsSpamAsync(long id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"tickets/{id}/mark_as_spam.json", null, "Zendesk.Tickets.MarkAsSpam",
            cancellationToken);

    /// <inheritdoc />
    public Task<ZendeskJobStatus> MarkManyAsSpamAsync(IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(ids.Count, nameof(ids));
        var requestUri = ZendeskQuery.Build("tickets/mark_many_as_spam.json", ("ids", string.Join(',', ids)));
        return SendJobAsync(HttpMethod.Put, requestUri, null, "Zendesk.Tickets.MarkManyAsSpam", cancellationToken);
    }

    /// <inheritdoc />
    public Task RestoreDeletedAsync(long id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"deleted_tickets/{id}/restore.json", null, "Zendesk.Tickets.RestoreDeleted",
            cancellationToken);

    /// <inheritdoc />
    public Task RestoreManyDeletedAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(ids.Count, nameof(ids));
        var requestUri = ZendeskQuery.Build("deleted_tickets/restore_many.json", ("ids", string.Join(',', ids)));
        return SendAsync(HttpMethod.Put, requestUri, null, "Zendesk.Tickets.RestoreManyDeleted", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskJobStatus> DeletePermanentlyAsync(long id, CancellationToken cancellationToken = default) =>
        // QUIRK: async job_status even for a single ticket.
        SendJobAsync(HttpMethod.Delete, $"deleted_tickets/{id}.json", null, "Zendesk.Tickets.DeletePermanently",
            cancellationToken);

    /// <inheritdoc />
    public Task<ZendeskJobStatus> DeleteManyPermanentlyAsync(IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(ids.Count, nameof(ids));
        var requestUri = ZendeskQuery.Build("deleted_tickets/destroy_many.json", ("ids", string.Join(',', ids)));
        return SendJobAsync(HttpMethod.Delete, requestUri, null, "Zendesk.Tickets.DeleteManyPermanently",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskTagNamesResult> SetTagsAsync(long ticketId, IReadOnlyList<string> tags,
        CancellationToken cancellationToken = default) =>
        PostAsync<ZendeskTagNamesResult>($"tickets/{ticketId}/tags.json", new { tags }, "Zendesk.Tickets.SetTags",
            cancellationToken);

    /// <inheritdoc />
    public Task<ZendeskTagNamesResult> AddTagsAsync(long ticketId, IReadOnlyList<string> tags,
        DateTimeOffset? updatedStamp = null, CancellationToken cancellationToken = default) =>
        PutAsync<ZendeskTagNamesResult>($"tickets/{ticketId}/tags.json", TagsPayload(tags, updatedStamp),
            "Zendesk.Tickets.AddTags", cancellationToken);

    /// <inheritdoc />
    public Task<ZendeskTagNamesResult> RemoveTagsAsync(long ticketId, IReadOnlyList<string> tags,
        DateTimeOffset? updatedStamp = null, CancellationToken cancellationToken = default)
    {
        // A DELETE with a JSON body — unusual, but that is the documented shape of the tag-removal endpoint.
        var payload = TagsPayload(tags, updatedStamp);
        return SendAsync<ZendeskTagNamesResult>(HttpMethod.Delete, $"tickets/{ticketId}/tags.json",
            JsonContent.Create(payload, payload.GetType(), options: WriteJsonOptions),
            "Zendesk.Tickets.RemoveTags", cancellationToken);
    }

    /// <inheritdoc />
    public Task MakeCommentPrivateAsync(long ticketId, long commentId,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"tickets/{ticketId}/comments/{commentId}/make_private.json", null,
            "Zendesk.Tickets.MakeCommentPrivate", cancellationToken);

    /// <inheritdoc />
    public async Task<ZendeskAttachment> RedactCommentAttachmentAsync(long ticketId, long commentId,
        long attachmentId, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<ZendeskAttachmentResponse>(HttpMethod.Put,
            $"tickets/{ticketId}/comments/{commentId}/attachments/{attachmentId}/redact.json", null,
            "Zendesk.Tickets.RedactCommentAttachment", cancellationToken).ConfigureAwait(false);
        return response.Attachment
               ?? throw new InvalidOperationException($"Zendesk returned no attachment for '{attachmentId}'.");
    }

    /// <inheritdoc />
    public async Task<ZendeskTicket> ImportAsync(ZendeskTicketImport ticket, bool archiveImmediately = false,
        CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("imports/tickets.json",
            ("archive_immediately", archiveImmediately ? "true" : null));
        var response = await PostAsync<ZendeskTicketResponse>(requestUri, new { ticket }, "Zendesk.Tickets.Import",
            cancellationToken).ConfigureAwait(false);
        return response.Ticket ?? throw new InvalidOperationException("Zendesk returned no imported ticket.");
    }

    /// <inheritdoc />
    public Task<ZendeskJobStatus> ImportManyAsync(IReadOnlyList<ZendeskTicketImport> tickets,
        bool archiveImmediately = false, CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(tickets.Count, nameof(tickets));
        var requestUri = ZendeskQuery.Build("imports/tickets/create_many.json",
            ("archive_immediately", archiveImmediately ? "true" : null));
        return SendJobAsync(HttpMethod.Post, requestUri, new { tickets }, "Zendesk.Tickets.ImportMany",
            cancellationToken);
    }

    // The tags docs pass safe_update as the string "true"; the stamp is the ticket's latest updated_at.
    private static object TagsPayload(IReadOnlyList<string> tags, DateTimeOffset? updatedStamp) => new
    {
        tags,
        updated_stamp = updatedStamp,
        safe_update = updatedStamp is null ? null : "true"
    };

    // Comments carry both a plain and a rich body; returning both doubles the tokens for a long thread. Trim to
    // the requested format (plain by default) so the caller pays for one representation unless it asks for both.
    private static ZendeskTicketCommentsResult ProjectBodies(ZendeskTicketCommentsResult result, string? bodyFormat)
    {
        var format = (bodyFormat ?? ZendeskCommentBodyFormats.Plain).Trim().ToLowerInvariant();
        if (format == ZendeskCommentBodyFormats.Both) return result;

        var comments = result.Comments
            .Select(comment => format == ZendeskCommentBodyFormats.Rich
                ? comment with { PlainBody = null }
                : comment with { Body = null })
            .ToList();
        return result with { Comments = comments };
    }

    [GeneratedRegex(@"(^|\s)type:", RegexOptions.IgnoreCase)]
    private static partial Regex TypeSelectorRegex();
}