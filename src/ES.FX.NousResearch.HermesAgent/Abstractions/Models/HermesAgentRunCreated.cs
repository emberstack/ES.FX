using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The <c>202 Accepted</c> acknowledgment returned by <c>POST /v1/runs</c>. Poll
///     <c>GET /v1/runs/{run_id}</c> or stream <c>GET /v1/runs/{run_id}/events</c> to follow the run.
/// </summary>
public sealed record HermesAgentRunCreated
{
    /// <summary>The identifier of the newly submitted run (<c>run_…</c>).</summary>
    [JsonPropertyName("run_id")]
    public string? RunId { get; init; }

    /// <summary>
    ///     The literal acknowledgment value <c>started</c>. It appears only in this body and is never a run
    ///     <c>status</c> value — see <see cref="Abstractions.HermesAgentRunStatuses" /> for the lifecycle
    ///     vocabulary.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}
