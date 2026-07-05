using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     A skill entry from the server's skills hub (see <c>GET /v1/skills</c>). Entries are open metadata
///     objects — the server guarantees at least <c>name</c>, <c>description</c> and <c>category</c>; any
///     additional metadata keys the hub attaches are ignored by this client.
/// </summary>
public sealed record HermesAgentSkill
{
    /// <summary>The skill name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The human-readable skill description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>The skill category.</summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }
}