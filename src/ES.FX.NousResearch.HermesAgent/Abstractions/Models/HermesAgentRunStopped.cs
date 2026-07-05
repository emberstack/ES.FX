using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The acknowledgment returned by <c>POST /v1/runs/{run_id}/stop</c>. The server reports
///     <c>stopping</c> even when the underlying interrupt raised; poll the run status to observe the terminal
///     outcome (typically <c>cancelled</c>).
/// </summary>
public sealed record HermesAgentRunStopped
{
    /// <summary>The identifier of the run being stopped.</summary>
    [JsonPropertyName("run_id")]
    public string? RunId { get; init; }

    /// <summary>The transient status <c>stopping</c> — see <see cref="Abstractions.HermesAgentRunStatuses" />.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}