using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools.Models;

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
    [Description(
        "checkbox fields only: tag added when checked; cannot collide with a tag/dropdown-option value used by " +
        "another custom field.")]
    [JsonPropertyName("tag")]
    public string? Tag { get; init; }

    /// <summary>
    ///     For <c>regexp</c> fields only — the validation pattern a field value must match to be deemed valid.
    /// </summary>
    [Description("regexp fields only: validation pattern.")]
    [JsonPropertyName("regexp_for_validation")]
    public string? RegexpForValidation { get; init; }

    /// <summary>
    ///     Drop-down/multiselect options. Required at creation for <c>tagger</c>/<c>multiselect</c> fields; each
    ///     option needs a <c>name</c> (label) and a <c>value</c> (tag), and is not used by other field types.
    ///     DESTRUCTIVE on update: the whole option set is replaced — omitted options are DELETED and their values
    ///     removed from tickets and macros, so include every option you want to keep.
    /// </summary>
    [JsonPropertyName("custom_field_options")]
    [Description(
        "Drop-down/multiselect options; required at creation for tagger and multiselect field types. Array order = display order.")]
    public IReadOnlyList<ZendeskCustomFieldOptionWrite>? CustomFieldOptions { get; init; }
}