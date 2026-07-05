using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The repeat bookkeeping of a stored job (<c>{"times": …, "completed": …}</c>). When
///     <see cref="Completed" /> reaches <see cref="Times" /> the job is DELETED from storage automatically
///     (a subsequent get returns <c>404</c>).
/// </summary>
public sealed record HermesAgentJobRepeat
{
    /// <summary>
    ///     The total number of runs before the job auto-deletes; <c>null</c> means run forever. Defaults to
    ///     <c>1</c> for one-shot schedules and <c>null</c> for interval/cron schedules when not set at create
    ///     time.
    /// </summary>
    [JsonPropertyName("times")]
    public int? Times { get; init; }

    /// <summary>The number of runs completed so far.</summary>
    [JsonPropertyName("completed")]
    public int? Completed { get; init; }
}
