using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>A page of macros (<c>GET /api/v2/macros</c>).</summary>
public sealed record ZendeskMacrosResult
{
    [JsonPropertyName("macros")] public IReadOnlyList<ZendeskMacro> Macros { get; init; } = [];
    [JsonPropertyName("count")] public int? Count { get; init; }
    [JsonPropertyName("next_page")] public string? NextPage { get; init; }
    [JsonPropertyName("previous_page")] public string? PreviousPage { get; init; }
}