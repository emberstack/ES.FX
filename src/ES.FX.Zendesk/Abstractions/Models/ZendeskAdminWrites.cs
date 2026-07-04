using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     The writable fields of a ticket field (create / update). <c>Type</c> is settable only at creation.
/// </summary>
public sealed record ZendeskTicketFieldWrite
{
    /// <summary>
    ///     The field type (<c>text</c>, <c>textarea</c>, <c>checkbox</c>, <c>date</c>, <c>integer</c>,
    ///     <c>decimal</c>, <c>regexp</c>, <c>multiselect</c>, <c>tagger</c>, <c>lookup</c>...). Required on
    ///     create; immutable afterwards.
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

    /// <summary>The tag applied by a <c>checkbox</c> field when checked.</summary>
    [JsonPropertyName("tag")]
    public string? Tag { get; init; }

    /// <summary>Validation pattern for a <c>regexp</c> field.</summary>
    [JsonPropertyName("regexp_for_validation")]
    public string? RegexpForValidation { get; init; }

    /// <summary>
    ///     Drop-down/multiselect options. Required on create for <c>tagger</c>/<c>multiselect</c>. DESTRUCTIVE on
    ///     update: the whole option set is replaced — omitted options are DELETED and their values removed from
    ///     tickets, so include every option you want to keep.
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

    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("value")] public string? Value { get; init; }
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

    /// <summary>The field ids on the form, in display order.</summary>
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
    ///     The macro actions (e.g. <c>{ "field": "status", "value": "solved" }</c>). Required on create.
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

    /// <summary>The brand subdomain. Required on create.</summary>
    [JsonPropertyName("subdomain")]
    public string? Subdomain { get; init; }

    [JsonPropertyName("active")] public bool? Active { get; init; }
    [JsonPropertyName("default")] public bool? Default { get; init; }
    [JsonPropertyName("brand_url")] public string? BrandUrl { get; init; }
    [JsonPropertyName("host_mapping")] public string? HostMapping { get; init; }

    [JsonPropertyName("signature_template")]
    public string? SignatureTemplate { get; init; }
}

/// <summary>The writable fields of a custom ticket status (create / update).</summary>
public sealed record ZendeskCustomStatusWrite
{
    /// <summary>
    ///     The built-in category — see <see cref="ZendeskStatusCategories" />. Required on create; NOT updatable
    ///     afterwards.
    /// </summary>
    [JsonPropertyName("status_category")]
    public string? StatusCategory { get; init; }

    /// <summary>The label agents see (max 48 chars). Required on create.</summary>
    [JsonPropertyName("agent_label")]
    public string? AgentLabel { get; init; }

    [JsonPropertyName("end_user_label")] public string? EndUserLabel { get; init; }
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
    ///     Conditions that must ALL match. On create, at least one condition on <c>status</c>, <c>type</c>,
    ///     <c>group_id</c>, <c>assignee_id</c>, or <c>requester_id</c> is required. DESTRUCTIVE on update:
    ///     condition arrays are replaced wholesale — send the complete set.
    /// </summary>
    [JsonPropertyName("all")]
    public IReadOnlyList<ZendeskViewCondition>? All { get; init; }

    /// <summary>Conditions of which ANY may match.</summary>
    [JsonPropertyName("any")]
    public IReadOnlyList<ZendeskViewCondition>? Any { get; init; }

    [JsonPropertyName("output")] public ZendeskViewOutput? Output { get; init; }
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