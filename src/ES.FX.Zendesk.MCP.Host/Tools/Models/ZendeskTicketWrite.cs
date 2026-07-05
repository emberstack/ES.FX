using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools.Models;

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

    /// <summary>
    ///     Sets a specific custom ticket status (see <see cref="CustomStatusId" /> description).
    /// </summary>
    [JsonPropertyName("custom_status_id")]
    [Description(
        "Sets a specific custom ticket status. When the account uses custom statuses, 'status' controls only the " +
        "category (new/open/pending/hold/solved/closed); set the exact status here (get valid ids from " +
        "custom_statuses_list).")]
    public long? CustomStatusId { get; init; }

    [JsonPropertyName("problem_id")] public long? ProblemId { get; init; }

    [JsonPropertyName("due_at")]
    [Description("Due timestamp (ISO8601). Only honored when type=task; ignored for other ticket types.")]
    public DateTimeOffset? DueAt { get; init; }

    [JsonPropertyName("external_id")] public string? ExternalId { get; init; }

    /// <summary>
    ///     Replaces the ticket's whole tag set. Prefer <see cref="AdditionalTags" /> / <see cref="RemoveTags" /> in bulk
    ///     updates.
    /// </summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Tags to add without overwriting (bulk <c>update_many</c> only; do not combine with <see cref="Tags" />).</summary>
    [JsonPropertyName("additional_tags")]
    [Description(
        "Tag names to APPEND without disturbing existing tags (unlike 'tags', which replaces the whole set). This is " +
        "the only way to edit tags on closed tickets. Only honored on bulk update — update_many or " +
        "update_many_batch (per item); ignored on single create/update — use 'tags' there.")]
    public IReadOnlyList<string>? AdditionalTags { get; init; }

    /// <summary>Tags to remove (bulk <c>update_many</c> only; do not combine with <see cref="Tags" />).</summary>
    [JsonPropertyName("remove_tags")]
    [Description(
        "Tag names to REMOVE without disturbing other tags (unlike 'tags', which replaces the whole set). Only " +
        "honored on bulk update — update_many or update_many_batch (per item).")]
    public IReadOnlyList<string>? RemoveTags { get; init; }

    [JsonPropertyName("collaborator_ids")] public IReadOnlyList<long>? CollaboratorIds { get; init; }

    [JsonPropertyName("custom_fields")]
    [Description(
        "Custom ticket-field values: an array of {id: <field id>, value: <type-dependent>}. The value shape depends " +
        "on the field's type — text/textarea: string; integer/decimal: number; checkbox: boolean; date: " +
        "\"YYYY-MM-DD\"; tagger (single-select dropdown): the option's TAG value (not its display text); multiselect: " +
        "an array of option tag values; regexp: a string matching the field's pattern; lookup: the related record's " +
        "id. Get each field's id and type from ticket_fields_list.")]
    public IReadOnlyList<ZendeskCustomFieldWrite>? CustomFields { get; init; }

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