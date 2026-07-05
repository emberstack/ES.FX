using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools.Models;

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