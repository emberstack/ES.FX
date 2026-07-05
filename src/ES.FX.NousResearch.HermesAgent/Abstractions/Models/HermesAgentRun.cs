using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The pollable status object of a run (<c>GET /v1/runs/{run_id}</c>). Fields accumulate as the run
///     progresses and are absent (<c>null</c>) until first set. Terminal statuses remain pollable for ~3600
///     seconds after <see cref="UpdatedAt" />; run state is held in server memory only.
/// </summary>
public sealed record HermesAgentRun
{
    /// <summary>The object discriminator (<c>hermes.run</c>).</summary>
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    /// <summary>The run identifier (<c>run_…</c>).</summary>
    [JsonPropertyName("run_id")]
    public string? RunId { get; init; }

    /// <summary>The run status — see <see cref="Abstractions.HermesAgentRunStatuses" />.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>When the run was created, as float unix seconds.</summary>
    [JsonPropertyName("created_at")]
    public double? CreatedAt { get; init; }

    /// <summary>When the run status was last updated, as float unix seconds.</summary>
    [JsonPropertyName("updated_at")]
    public double? UpdatedAt { get; init; }

    /// <summary>The Hermes session id the run belongs to.</summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    /// <summary>The model name recorded for the run.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>The name of the most recent run event (e.g. <c>run.completed</c>).</summary>
    [JsonPropertyName("last_event")]
    public string? LastEvent { get; init; }

    /// <summary>The final output text — present only once the run completed.</summary>
    [JsonPropertyName("output")]
    public string? Output { get; init; }

    /// <summary>The token usage of the run, when available.</summary>
    [JsonPropertyName("usage")]
    public HermesAgentUsage? Usage { get; init; }

    /// <summary>The (server-redacted) error message — present only once the run failed.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}