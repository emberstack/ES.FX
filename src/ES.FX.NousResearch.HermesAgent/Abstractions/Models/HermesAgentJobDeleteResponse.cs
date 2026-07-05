using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The <c>{ "ok": true }</c> acknowledgement returned by <c>DELETE /api/jobs/{job_id}</c> (the jobs
///     surface responds <c>200</c> with this body instead of <c>204</c>).
/// </summary>
internal sealed record HermesAgentJobDeleteResponse
{
    /// <summary>Whether the delete was acknowledged.</summary>
    [JsonPropertyName("ok")]
    public bool? Ok { get; init; }
}