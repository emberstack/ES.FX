using System.Text.Json;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A single event within a ticket audit (e.g. a field change, a comment, a notification). Events are
///     heterogeneous; <see cref="Value" />/<see cref="PreviousValue" /> can be a string, array, or object.
/// </summary>
public sealed record ZendeskAuditEvent
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("field_name")] public string? FieldName { get; init; }
    [JsonPropertyName("value")] public JsonElement? Value { get; init; }
    [JsonPropertyName("previous_value")] public JsonElement? PreviousValue { get; init; }
    [JsonPropertyName("author_id")] public long? AuthorId { get; init; }
    [JsonPropertyName("body")] public string? Body { get; init; }
    [JsonPropertyName("public")] public bool? Public { get; init; }
}