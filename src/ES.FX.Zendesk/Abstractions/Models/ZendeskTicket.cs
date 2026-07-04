using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A Zendesk ticket (see <c>GET /api/v2/tickets/{id}.json</c>).
/// </summary>
public sealed record ZendeskTicket
{
    /// <summary>The automatically assigned ticket identifier.</summary>
    [JsonPropertyName("id")]
    public long Id { get; init; }

    /// <summary>The API URL of the ticket.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    /// <summary>The value of the subject field.</summary>
    [JsonPropertyName("subject")]
    public string? Subject { get; init; }

    /// <summary>The first comment / description of the ticket.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>The state of the ticket (<c>new</c>, <c>open</c>, <c>pending</c>, <c>hold</c>, <c>solved</c>, <c>closed</c>).</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>The priority (<c>low</c>, <c>normal</c>, <c>high</c>, <c>urgent</c>).</summary>
    [JsonPropertyName("priority")]
    public string? Priority { get; init; }

    /// <summary>The type (<c>problem</c>, <c>incident</c>, <c>question</c>, <c>task</c>).</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The id of the user who requested the ticket.</summary>
    [JsonPropertyName("requester_id")]
    public long? RequesterId { get; init; }

    /// <summary>The id of the user who submitted the ticket.</summary>
    [JsonPropertyName("submitter_id")]
    public long? SubmitterId { get; init; }

    /// <summary>The id of the agent the ticket is assigned to.</summary>
    [JsonPropertyName("assignee_id")]
    public long? AssigneeId { get; init; }

    /// <summary>The organization id of the requester.</summary>
    [JsonPropertyName("organization_id")]
    public long? OrganizationId { get; init; }

    /// <summary>The group the ticket is assigned to.</summary>
    [JsonPropertyName("group_id")]
    public long? GroupId { get; init; }

    /// <summary>The id of the ticket form.</summary>
    [JsonPropertyName("ticket_form_id")]
    public long? TicketFormId { get; init; }

    /// <summary>The id of the brand.</summary>
    [JsonPropertyName("brand_id")]
    public long? BrandId { get; init; }

    /// <summary>The id of the custom ticket status, if custom statuses are enabled.</summary>
    [JsonPropertyName("custom_status_id")]
    public long? CustomStatusId { get; init; }

    /// <summary>
    ///     The custom field values stored on the ticket (raw id + value). Decode the ids to titles/option labels
    ///     with <c>zendesk_ticket_fields_list</c> / <c>zendesk_ticket_fields_read</c>.
    /// </summary>
    [JsonPropertyName("custom_fields")]
    public IReadOnlyList<ZendeskTicketCustomFieldValue>? CustomFields { get; init; }

    /// <summary>The ids of agents added as collaborators (CC'd) on the ticket.</summary>
    [JsonPropertyName("collaborator_ids")]
    public IReadOnlyList<long>? CollaboratorIds { get; init; }

    /// <summary>The ids of end users CC'd on the ticket (email CCs).</summary>
    [JsonPropertyName("email_cc_ids")]
    public IReadOnlyList<long>? EmailCcIds { get; init; }

    /// <summary>The ids of agents following the ticket.</summary>
    [JsonPropertyName("follower_ids")]
    public IReadOnlyList<long>? FollowerIds { get; init; }

    /// <summary>The tags applied to the ticket.</summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>The channel the ticket was created through (e.g. <c>email</c>, <c>web</c>, <c>api</c>).</summary>
    [JsonPropertyName("via")]
    public ZendeskVia? Via { get; init; }

    /// <summary>The customer satisfaction (CSAT) rating on the ticket, if any.</summary>
    [JsonPropertyName("satisfaction_rating")]
    public ZendeskSatisfactionRating? SatisfactionRating { get; init; }

    /// <summary>For an <c>incident</c> ticket, the id of the <c>problem</c> ticket it is linked to.</summary>
    [JsonPropertyName("problem_id")]
    public long? ProblemId { get; init; }

    /// <summary>For a <c>problem</c> ticket, whether any incidents are linked to it (see <c>zendesk_tickets_incidents</c>).</summary>
    [JsonPropertyName("has_incidents")]
    public bool? HasIncidents { get; init; }

    /// <summary>Whether the ticket is public.</summary>
    [JsonPropertyName("is_public")]
    public bool? IsPublic { get; init; }

    /// <summary>
    ///     The number of public comments — populated only when the request sideloads <c>comment_count</c>
    ///     (<c>include=comment_count</c>).
    /// </summary>
    [JsonPropertyName("comment_count")]
    public long? CommentCount { get; init; }

    /// <summary>An external id for linking to a system outside Zendesk.</summary>
    [JsonPropertyName("external_id")]
    public string? ExternalId { get; init; }

    /// <summary>When the ticket was created.</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>When the ticket was last updated.</summary>
    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>When the (task-type) ticket is due.</summary>
    [JsonPropertyName("due_at")]
    public DateTimeOffset? DueAt { get; init; }
}