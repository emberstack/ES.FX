using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     The writable fields of a ticket, used for both create (<c>POST /api/v2/tickets.json</c>) and update
///     (<c>PUT /api/v2/tickets/{id}.json</c>). Unset (<c>null</c>) properties are omitted from the request, so an
///     update sends only the fields you set.
/// </summary>
public sealed record ZendeskTicketWrite
{
    /// <summary>The ticket id — set ONLY for batch <c>update_many</c> items; leave <c>null</c> everywhere else.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; init; }

    [JsonPropertyName("subject")] public string? Subject { get; init; }

    /// <summary>
    ///     The comment to add. On create, this becomes the ticket description (Zendesk effectively requires it);
    ///     on update it appends a reply (<c>Public = true</c>) or internal note (<c>Public = false</c>).
    /// </summary>
    [JsonPropertyName("comment")]
    public ZendeskTicketCommentWrite? Comment { get; init; }

    /// <summary>
    ///     The status — one of new, open, pending, hold, solved, closed (see
    ///     <see cref="ZendeskTicketStatuses" />).
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>
    ///     The priority — one of low, normal, high, urgent (see <see cref="ZendeskTicketPriorities" />).
    /// </summary>
    [JsonPropertyName("priority")]
    public string? Priority { get; init; }

    /// <summary>
    ///     The type — one of problem, incident, question, task (see <see cref="ZendeskTicketTypes" />).
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("requester_id")] public long? RequesterId { get; init; }
    [JsonPropertyName("assignee_id")] public long? AssigneeId { get; init; }
    [JsonPropertyName("group_id")] public long? GroupId { get; init; }
    [JsonPropertyName("organization_id")] public long? OrganizationId { get; init; }
    [JsonPropertyName("brand_id")] public long? BrandId { get; init; }
    [JsonPropertyName("ticket_form_id")] public long? TicketFormId { get; init; }
    [JsonPropertyName("custom_status_id")] public long? CustomStatusId { get; init; }
    [JsonPropertyName("problem_id")] public long? ProblemId { get; init; }
    [JsonPropertyName("due_at")] public DateTimeOffset? DueAt { get; init; }
    [JsonPropertyName("external_id")] public string? ExternalId { get; init; }

    /// <summary>
    ///     Replaces the ticket's whole tag set. Prefer <see cref="AdditionalTags" /> / <see cref="RemoveTags" /> in bulk
    ///     updates.
    /// </summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Tags to add without overwriting (bulk <c>update_many</c> only; do not combine with <see cref="Tags" />).</summary>
    [JsonPropertyName("additional_tags")]
    public IReadOnlyList<string>? AdditionalTags { get; init; }

    /// <summary>Tags to remove (bulk <c>update_many</c> only; do not combine with <see cref="Tags" />).</summary>
    [JsonPropertyName("remove_tags")]
    public IReadOnlyList<string>? RemoveTags { get; init; }

    [JsonPropertyName("collaborator_ids")] public IReadOnlyList<long>? CollaboratorIds { get; init; }

    [JsonPropertyName("custom_fields")] public IReadOnlyList<ZendeskCustomFieldWrite>? CustomFields { get; init; }

    /// <summary>
    ///     Optimistic-locking guard: set <c>true</c> together with <see cref="UpdatedStamp" /> (the ticket's latest
    ///     <c>updated_at</c>) so a concurrent modification fails with <c>409 Conflict</c> instead of overwriting.
    ///     Since 2025-05-15 Zendesk also 409s plain concurrent ticket updates.
    /// </summary>
    [JsonPropertyName("safe_update")]
    public bool? SafeUpdate { get; init; }

    /// <summary>The ticket's latest <c>updated_at</c>, required when <see cref="SafeUpdate" /> is set.</summary>
    [JsonPropertyName("updated_stamp")]
    public DateTimeOffset? UpdatedStamp { get; init; }
}

/// <summary>A comment payload on a ticket create/update.</summary>
public sealed record ZendeskTicketCommentWrite
{
    /// <summary>The plain-text body (use exactly one of <see cref="Body" /> / <see cref="HtmlBody" />).</summary>
    [JsonPropertyName("body")]
    public string? Body { get; init; }

    /// <summary>The HTML body (use exactly one of <see cref="Body" /> / <see cref="HtmlBody" />).</summary>
    [JsonPropertyName("html_body")]
    public string? HtmlBody { get; init; }

    /// <summary><c>true</c> for a public reply, <c>false</c> for an internal note. Zendesk defaults to public.</summary>
    [JsonPropertyName("public")]
    public bool? Public { get; init; }

    /// <summary>The comment author; defaults to the authenticated user.</summary>
    [JsonPropertyName("author_id")]
    public long? AuthorId { get; init; }

    /// <summary>Upload tokens (from <c>IZendeskUploadsApi.UploadAsync</c>) attaching files to the comment.</summary>
    [JsonPropertyName("uploads")]
    public IReadOnlyList<string>? Uploads { get; init; }
}

/// <summary>A custom field value on a ticket write (<c>{ "id": ..., "value": ... }</c>).</summary>
public sealed record ZendeskCustomFieldWrite
{
    [JsonPropertyName("id")] public long Id { get; init; }

    /// <summary>The value — string, number, bool, or array of strings, matching the field type.</summary>
    [JsonPropertyName("value")]
    public object? Value { get; init; }
}

/// <summary>
///     The response of a ticket update (<c>PUT /api/v2/tickets/{id}.json</c>), which carries both the updated
///     ticket and the audit describing the change.
/// </summary>
public sealed record ZendeskTicketUpdateResult
{
    [JsonPropertyName("ticket")] public ZendeskTicket? Ticket { get; init; }
    [JsonPropertyName("audit")] public ZendeskTicketAudit? Audit { get; init; }
}