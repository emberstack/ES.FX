using System.Text.Json;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A single custom field value stored on a ticket: the field's <see cref="Id" /> plus its raw
///     <see cref="Value" />. The value is heterogeneous (string, number, boolean, or array), so it is exposed as a
///     <see cref="JsonElement" />. Resolve the id to a human title and decode option values with
///     <c>zendesk_ticket_fields_list</c> / <c>zendesk_ticket_fields_read</c>.
/// </summary>
public sealed record ZendeskTicketCustomFieldValue
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("value")] public JsonElement? Value { get; init; }
}