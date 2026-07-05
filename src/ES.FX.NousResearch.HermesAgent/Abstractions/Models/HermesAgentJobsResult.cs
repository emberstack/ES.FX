using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The <c>{ "jobs": [ ... ] }</c> envelope returned by <c>GET /api/jobs</c>. No pagination and no total
///     count — the array is the complete (filtered) job set in storage order.
/// </summary>
internal sealed record HermesAgentJobsResult
{
    /// <summary>The wrapped jobs (may be empty).</summary>
    [JsonPropertyName("jobs")]
    public IReadOnlyList<HermesAgentJob> Jobs { get; init; } = [];
}
