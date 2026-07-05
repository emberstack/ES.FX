using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     A toolset advertised by the server (see <c>GET /v1/toolsets</c>).
/// </summary>
public sealed record HermesAgentToolset
{
    /// <summary>The toolset name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The human-readable display label.</summary>
    [JsonPropertyName("label")]
    public string? Label { get; init; }

    /// <summary>The toolset description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Whether the toolset is enabled on the server.</summary>
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }

    /// <summary>Whether the toolset is configured (has whatever credentials/settings it needs).</summary>
    [JsonPropertyName("configured")]
    public bool? Configured { get; init; }

    /// <summary>
    ///     The concrete tool names of the toolset (sorted and de-duplicated by the server). A toolset that fails
    ///     to resolve server-side still appears in the listing, with an empty list here.
    /// </summary>
    [JsonPropertyName("tools")]
    public IReadOnlyList<string> Tools { get; init; } = [];
}