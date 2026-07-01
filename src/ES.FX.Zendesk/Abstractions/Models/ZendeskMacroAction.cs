using System.Text.Json;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     An action a macro applies (e.g. set a field, add a tag, or set the reply comment). <see cref="Value" /> may
///     be a string or an array depending on the field.
/// </summary>
public sealed record ZendeskMacroAction
{
    [JsonPropertyName("field")] public string? Field { get; init; }
    [JsonPropertyName("value")] public JsonElement? Value { get; init; }
}