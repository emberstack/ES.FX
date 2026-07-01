using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     An option for a dropdown/tagger custom field. <see cref="Value" /> is the tag stored on records;
///     <see cref="Name" /> is the human-readable label.
/// </summary>
public sealed record ZendeskCustomFieldOption
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("raw_name")] public string? RawName { get; init; }
    [JsonPropertyName("value")] public string? Value { get; init; }
    [JsonPropertyName("default")] public bool? Default { get; init; }
}