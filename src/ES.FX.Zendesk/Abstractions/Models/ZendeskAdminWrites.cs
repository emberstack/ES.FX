using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     The writable fields of a ticket field (create / update). <c>Type</c> is settable only at creation.
/// </summary>
public sealed record ZendeskTicketFieldWrite
{
    /// <summary>
    ///     The field type. Required on create and immutable afterwards. Allowed custom types: <c>text</c> (the
    ///     default if omitted), <c>textarea</c>, <c>checkbox</c>, <c>date</c>, <c>integer</c>, <c>decimal</c>,
    ///     <c>regexp</c>, <c>partialcreditcard</c>, <c>multiselect</c>, <c>tagger</c> (single-select dropdown), and
    ///     <c>lookup</c> (a relationship to another object).
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The field title. Required on create.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("position")] public long? Position { get; init; }
    [JsonPropertyName("active")] public bool? Active { get; init; }
    [JsonPropertyName("required")] public bool? Required { get; init; }

    [JsonPropertyName("visible_in_portal")]
    public bool? VisibleInPortal { get; init; }

    [JsonPropertyName("editable_in_portal")]
    public bool? EditableInPortal { get; init; }

    [JsonPropertyName("required_in_portal")]
    public bool? RequiredInPortal { get; init; }

    /// <summary>
    ///     For <c>checkbox</c> fields only — the tag added to a ticket when the box is checked. Tags cannot be
    ///     reused across custom ticket fields (a tag used here can't also be a drop-down option value).
    /// </summary>
    [JsonPropertyName("tag")]
    public string? Tag { get; init; }

    /// <summary>
    ///     For <c>regexp</c> fields only — the validation pattern a field value must match to be deemed valid.
    /// </summary>
    [JsonPropertyName("regexp_for_validation")]
    public string? RegexpForValidation { get; init; }

    /// <summary>
    ///     Drop-down/multiselect options. Required at creation for <c>tagger</c>/<c>multiselect</c> fields; each
    ///     option needs a <c>name</c> (label) and a <c>value</c> (tag), and is not used by other field types.
    ///     DESTRUCTIVE on update: the whole option set is replaced — omitted options are DELETED and their values
    ///     removed from tickets and macros, so include every option you want to keep.
    /// </summary>
    [JsonPropertyName("custom_field_options")]
    public IReadOnlyList<ZendeskCustomFieldOptionWrite>? CustomFieldOptions { get; init; }
}

/// <summary>The writable fields of a custom field option (upsert: include <see cref="Id" /> to update).</summary>
public sealed record ZendeskCustomFieldOptionWrite
{
    /// <summary>Include to UPDATE an existing option; omit to create one.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; init; }

    /// <summary>The human-readable label for the option. Required together with <see cref="Value" />.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    ///     The tag stored on tickets that select this option. Required together with <see cref="Name" />. A value
    ///     cannot collide with a tag used by a <c>checkbox</c> field, since tags aren't reusable across custom
    ///     fields.
    /// </summary>
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("position")] public long? Position { get; init; }

    /// <summary>
    ///     Whether a ticket can be solved while this option is selected. Include it when replacing a field's
    ///     option set — omitted options are recreated with the Zendesk default otherwise.
    /// </summary>
    [JsonPropertyName("allow_solving")]
    public bool? AllowSolving { get; init; }
}

/// <summary>The <c>{ "custom_field_option": {...} }</c> envelope.</summary>
public sealed record ZendeskCustomFieldOptionResponse
{
    [JsonPropertyName("custom_field_option")]
    public ZendeskCustomFieldOption? CustomFieldOption { get; init; }
}

/// <summary>The writable fields of a ticket form (create / update).</summary>
public sealed record ZendeskTicketFormWrite
{
    /// <summary>The form name. Required on create.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("display_name")] public string? DisplayName { get; init; }
    [JsonPropertyName("position")] public long? Position { get; init; }
    [JsonPropertyName("active")] public bool? Active { get; init; }
    [JsonPropertyName("default")] public bool? Default { get; init; }
    [JsonPropertyName("end_user_visible")] public bool? EndUserVisible { get; init; }
    [JsonPropertyName("in_all_brands")] public bool? InAllBrands { get; init; }

    /// <summary>
    ///     The field ids on the form, in display order. Supplying this replaces the form's field list wholesale in
    ///     display order — read the current form with forms_get first and send the complete ordered list, or
    ///     existing fields are dropped.
    /// </summary>
    [JsonPropertyName("ticket_field_ids")]
    public IReadOnlyList<long>? TicketFieldIds { get; init; }
}

/// <summary>The writable fields of a macro (create / update).</summary>
public sealed record ZendeskMacroWrite
{
    /// <summary>The macro title. Required on create.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    ///     The macro actions. Required on create. Each action is a <c>{ field, value }</c> pair where
    ///     <c>field</c> names a ticket field to modify (e.g. <c>"status"</c>) and <c>value</c> is its new value
    ///     (e.g. <c>"solved"</c>); <c>value</c> is a string, or an array of strings for multi-value actions.
    ///     DESTRUCTIVE on update: the whole array is replaced — include ALL actions when changing any one.
    /// </summary>
    [JsonPropertyName("actions")]
    public IReadOnlyList<ZendeskMacroActionWrite>? Actions { get; init; }

    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("active")] public bool? Active { get; init; }
    [JsonPropertyName("position")] public long? Position { get; init; }
}

/// <summary>A macro action on a write (<c>{ "field": ..., "value": ... }</c>; see the actions reference).</summary>
public sealed record ZendeskMacroActionWrite
{
    [JsonPropertyName("field")] public string? Field { get; init; }

    /// <summary>The action value — a string, or an array of strings for multi-value actions.</summary>
    [JsonPropertyName("value")]
    public object? Value { get; init; }
}

/// <summary>The writable fields of a brand (create / update).</summary>
public sealed record ZendeskBrandWrite
{
    /// <summary>The brand name. Required on create.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    ///     The brand subdomain — the {subdomain}.zendesk.com host segment, not a full URL. Required on create.
    /// </summary>
    [JsonPropertyName("subdomain")]
    public string? Subdomain { get; init; }

    [JsonPropertyName("active")] public bool? Active { get; init; }

    /// <summary>
    ///     Marks this brand as the account default. Only one brand can be default; setting this true moves the
    ///     default off the previous brand.
    /// </summary>
    [JsonPropertyName("default")]
    public bool? Default { get; init; }

    [JsonPropertyName("brand_url")] public string? BrandUrl { get; init; }

    /// <summary>The custom host (CNAME) mapped to this brand, if any. Only admins can view this property.</summary>
    [JsonPropertyName("host_mapping")]
    public string? HostMapping { get; init; }

    [JsonPropertyName("signature_template")]
    public string? SignatureTemplate { get; init; }
}

/// <summary>The writable fields of a custom ticket status (create / update).</summary>
public sealed record ZendeskCustomStatusWrite
{
    /// <summary>
    ///     The built-in category — see <see cref="ZendeskStatusCategories" />. Allowed values: new, open, pending,
    ///     hold, solved. Required on create; immutable afterwards (not accepted on update).
    /// </summary>
    [JsonPropertyName("status_category")]
    public string? StatusCategory { get; init; }

    /// <summary>The label agents see (max 48 chars). Required on create.</summary>
    [JsonPropertyName("agent_label")]
    public string? AgentLabel { get; init; }

    /// <summary>The label end users see (max 48 chars).</summary>
    [JsonPropertyName("end_user_label")]
    public string? EndUserLabel { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }

    [JsonPropertyName("end_user_description")]
    public string? EndUserDescription { get; init; }

    [JsonPropertyName("active")] public bool? Active { get; init; }
}

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
    [JsonPropertyName("all")]
    public IReadOnlyList<ZendeskViewCondition>? All { get; init; }

    /// <summary>
    ///     Conditions of which ANY may match. At least one <see cref="All" /> condition must also be defined when
    ///     using <c>any</c> conditions. DESTRUCTIVE on update: a PUT replaces the entire array — send the complete set.
    /// </summary>
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

/// <summary>A view condition (see Zendesk's conditions reference for the field/operator vocabulary).</summary>
public sealed record ZendeskViewCondition
{
    [JsonPropertyName("field")] public string? Field { get; init; }
    [JsonPropertyName("operator")] public string? Operator { get; init; }
    [JsonPropertyName("value")] public string? Value { get; init; }
}

/// <summary>The output layout of a view (max 10 columns).</summary>
public sealed record ZendeskViewOutput
{
    [JsonPropertyName("columns")] public IReadOnlyList<string>? Columns { get; init; }
    [JsonPropertyName("group_by")] public string? GroupBy { get; init; }
    [JsonPropertyName("group_order")] public string? GroupOrder { get; init; }
    [JsonPropertyName("sort_by")] public string? SortBy { get; init; }
    [JsonPropertyName("sort_order")] public string? SortOrder { get; init; }
}