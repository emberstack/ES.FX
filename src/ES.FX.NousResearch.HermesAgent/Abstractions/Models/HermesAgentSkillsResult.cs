using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The <c>{"object":"list","data":[...]}</c> envelope returned by <c>GET /v1/skills</c>. Used internally by
///     the Server area implementation and unwrapped to the skill list.
/// </summary>
internal sealed record HermesAgentSkillsResult
{
    /// <summary>The object type discriminator (always <c>list</c>).</summary>
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    /// <summary>The skill entries.</summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<HermesAgentSkill> Data { get; init; } = [];
}
