using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A ticket field definition (<c>GET /api/v2/ticket_fields</c>). Needed to interpret the custom field values
///     stored on a ticket: maps a field id to a title, type, and (for dropdowns) option value→label pairs.
/// </summary>
public sealed record ZendeskTicketField
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }

    /// <summary>The field type (e.g. <c>text</c>, <c>tagger</c>, <c>multiselect</c>, <c>checkbox</c>, <c>date</c>).</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("raw_title")] public string? RawTitle { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("active")] public bool Active { get; init; }
    [JsonPropertyName("required")] public bool? Required { get; init; }
    [JsonPropertyName("position")] public int? Position { get; init; }

    /// <summary>For dropdown/tagger fields, the available options.</summary>
    [JsonPropertyName("custom_field_options")]
    public IReadOnlyList<ZendeskCustomFieldOption>? CustomFieldOptions { get; init; }

    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
}