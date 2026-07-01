using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>Envelope for a single-macro response (<c>{ "macro": { ... } }</c>).</summary>
public sealed record ZendeskMacroResponse
{
    [JsonPropertyName("macro")] public ZendeskMacro? Macro { get; init; }
}