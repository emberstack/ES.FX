using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The <c>{ "job": { ... } }</c> envelope returned by the single-job endpoints (create, get, update,
///     pause, resume and trigger).
/// </summary>
internal sealed record HermesAgentJobResponse
{
    /// <summary>The wrapped job.</summary>
    [JsonPropertyName("job")]
    public HermesAgentJob? Job { get; init; }
}