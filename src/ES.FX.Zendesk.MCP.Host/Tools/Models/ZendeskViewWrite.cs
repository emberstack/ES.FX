using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools.Models;

/// <summary>
///     The writable fields of a view. NOTE: the write shape differs from the read shape — writes use top-level
///     <see cref="All" />/<see cref="Any" /> condition arrays plus <see cref="Output" />, while reads return
///     <c>conditions</c>/<c>execution</c> objects.
/// </summary>
public sealed record ZendeskViewWrite
{
    /// <summary>The view title. Required on create.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("active")] public bool? Active { get; init; }

    /// <summary>
    ///     Conditions that must ALL match. Each condition is a <c>{ field, operator, value }</c> triple; see
    ///     Zendesk's conditions reference for the field/operator vocabulary. On create, at least one condition on
    ///     <c>status</c>, <c>type</c>, <c>group_id</c>, <c>assignee_id</c>, or <c>requester_id</c> is required.
    ///     DESTRUCTIVE on update: a PUT replaces the entire array — send the complete set. Whenever <see cref="Any" />
    ///     conditions are present, at least one <c>all</c> condition must remain.
    /// </summary>
    [Description(
        "Conditions that must ALL match, as { field, operator, value } triples. On create at least one 'all' " +
        "condition must check status, type, group_id, assignee_id, or requester_id. Common fields (values are " +
        "strings): status is|is_not|less_than|greater_than one of new/open/pending/hold/solved/closed; type " +
        "is|is_not one of question/incident/problem/task; priority is|is_not|less_than|greater_than one of " +
        "\"\"/low/normal/high/urgent; group_id/assignee_id/requester_id/organization_id is|is_not with a numeric " +
        "id string (assignee_id/requester_id also accept \"current_user\"; all accept \"\" for none); current_tags " +
        "includes|not_includes with a space-delimited tag string; brand_id/ticket_form_id/locale_id is|is_not with " +
        "a numeric id string; custom_status_id includes|not_includes with a numeric status-id string; via_id " +
        "is|is_not with a numeric channel id (0=web_form, 4=mail, 5=API, 29=chat, 48=web_widget); a custom field is " +
        "field \"custom_fields_{id}\" (operators depend on the field type). Time fields " +
        "(NEW/OPEN/PENDING/SOLVED/CLOSED/assigned_at/updated_at/requester_updated_at/assignee_updated_at/due_date, " +
        "plus *_business_hours variants) use is|less_than|greater_than with an integer hours value. This is the " +
        "common set — for the exact fields/operators your tenant allows, inspect an existing view with views_get.")]
    [JsonPropertyName("all")]
    public IReadOnlyList<ZendeskViewCondition>? All { get; init; }

    /// <summary>
    ///     Conditions of which ANY may match. At least one <see cref="All" /> condition must also be defined when
    ///     using <c>any</c> conditions. DESTRUCTIVE on update: a PUT replaces the entire array — send the complete set.
    /// </summary>
    [Description(
        "Conditions of which ANY may match — same { field, operator, value } shape and vocabulary as 'all'. " +
        "Whenever you supply 'any' conditions you must ALSO supply at least one 'all' condition, or the API " +
        "rejects the request.")]
    [JsonPropertyName("any")]
    public IReadOnlyList<ZendeskViewCondition>? Any { get; init; }

    /// <summary>
    ///     Output layout. <c>columns</c> takes up to 10 column values from the View columns vocabulary (e.g.
    ///     <c>status</c>, <c>description</c>=Subject, <c>priority</c>, <c>requester</c>, <c>assignee</c>,
    ///     <c>created</c>=Requested, <c>updated</c>, <c>group</c>, <c>organization</c>, <c>type</c>,
    ///     <c>ticket_form</c>, <c>custom_status_id</c>; for a custom field use its numeric id). <c>group_by</c>/
    ///     <c>sort_by</c> must reference a View column; <c>description</c>, <c>submitter</c>, and
    ///     <c>custom_status_id</c> are NOT supported for grouping/sorting. <c>group_order</c>/<c>sort_order</c> are
    ///     each "asc" or "desc".
    /// </summary>
    [JsonPropertyName("output")]
    public ZendeskViewOutput? Output { get; init; }
}