using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     A stored Hermes Agent scheduled job (the "hermes cron shape") as returned by every <c>/api/jobs</c>
///     endpoint. Timestamps are ISO-8601 strings carrying the server's configured Hermes timezone offset; there
///     is no per-job <c>updated_at</c> and no per-job timezone. The server may add fields over time — unknown
///     fields are ignored on deserialization.
/// </summary>
public sealed record HermesAgentJob
{
    /// <summary>The job id (exactly 12 lowercase hex characters). Immutable once created.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    ///     The job name (max 200 characters). When omitted at create time the server derives one from the
    ///     prompt, first skill or script.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The agent prompt executed on each run (<c>""</c> when the job was created without one).</summary>
    [JsonPropertyName("prompt")]
    public string? Prompt { get; init; }

    /// <summary>The skills injected for the run (ordered, unique, trimmed).</summary>
    [JsonPropertyName("skills")]
    public IReadOnlyList<string> Skills { get; init; } = [];

    /// <summary>Legacy single-skill mirror — kept in sync with the first entry of <see cref="Skills" />.</summary>
    [JsonPropertyName("skill")]
    public string? Skill { get; init; }

    /// <summary>The per-job model override; <c>null</c> resolves the global default at fire time.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>The per-job provider override; <c>null</c> resolves the global default at fire time.</summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    /// <summary>
    ///     Drift guard: the resolution of the global provider captured at create time when
    ///     <see cref="Provider" /> is unpinned (<c>null</c>). If the global default later changes, the run fails
    ///     closed. <c>null</c> for pinned jobs, script-only jobs, resolution failures or pre-feature jobs.
    /// </summary>
    [JsonPropertyName("provider_snapshot")]
    public string? ProviderSnapshot { get; init; }

    /// <summary>Drift guard snapshot for <see cref="Model" /> — same semantics as <see cref="ProviderSnapshot" />.</summary>
    [JsonPropertyName("model_snapshot")]
    public string? ModelSnapshot { get; init; }

    /// <summary>The per-job API base URL override (trailing <c>/</c> stripped by the server), if any.</summary>
    [JsonPropertyName("base_url")]
    public string? BaseUrl { get; init; }

    /// <summary>The pre-run script name, if any. Not settable over the REST surface (CLI/agent-tool only).</summary>
    [JsonPropertyName("script")]
    public string? Script { get; init; }

    /// <summary>Whether the job is script-only (runs without an agent; requires <see cref="Script" />).</summary>
    [JsonPropertyName("no_agent")]
    public bool? NoAgent { get; init; }

    /// <summary>Upstream job ids whose latest output is injected as context for this job's runs, if any.</summary>
    [JsonPropertyName("context_from")]
    public IReadOnlyList<string>? ContextFrom { get; init; }

    /// <summary>
    ///     The parsed schedule. Jobs are written with a schedule STRING
    ///     (<see cref="HermesAgentJobWrite.Schedule" />); the server stores and returns this structured form.
    /// </summary>
    [JsonPropertyName("schedule")]
    public HermesAgentJobSchedule? Schedule { get; init; }

    /// <summary>The human-readable schedule description (falls back to the raw string; <c>?</c> when nothing is derivable).</summary>
    [JsonPropertyName("schedule_display")]
    public string? ScheduleDisplay { get; init; }

    /// <summary>
    ///     The repeat bookkeeping (<c>{times, completed}</c>). The job deletes itself from storage when the
    ///     completed count reaches the configured total — see <see cref="HermesAgentJobRepeat" />.
    /// </summary>
    [JsonPropertyName("repeat")]
    public HermesAgentJobRepeat? Repeat { get; init; }

    /// <summary>
    ///     Whether the job is eligible for scheduling. <c>false</c> for paused or disabled jobs — such jobs are
    ///     hidden from the default job listing.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }

    /// <summary>The job state — see <see cref="HermesAgentJobStates" />.</summary>
    [JsonPropertyName("state")]
    public string? State { get; init; }

    /// <summary>When the job was paused, if it is currently paused.</summary>
    [JsonPropertyName("paused_at")]
    public DateTimeOffset? PausedAt { get; init; }

    /// <summary>The pause reason, if any (always <c>null</c> when paused over the REST surface).</summary>
    [JsonPropertyName("paused_reason")]
    public string? PausedReason { get; init; }

    /// <summary>When the job was created.</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>The next scheduled run; <c>null</c> when no further runs are expected.</summary>
    [JsonPropertyName("next_run_at")]
    public DateTimeOffset? NextRunAt { get; init; }

    /// <summary>When the job last ran; <c>null</c> before the first run.</summary>
    [JsonPropertyName("last_run_at")]
    public DateTimeOffset? LastRunAt { get; init; }

    /// <summary>The status of the last run — see <see cref="HermesAgentJobLastRunStatuses" />.</summary>
    [JsonPropertyName("last_status")]
    public string? LastStatus { get; init; }

    /// <summary>The error text of the last failed run; cleared on the next successful run.</summary>
    [JsonPropertyName("last_error")]
    public string? LastError { get; init; }

    /// <summary>
    ///     The last delivery failure, tracked separately from run failure (delivery targets are not validated
    ///     at create/update time — a bad target shows up here after a run).
    /// </summary>
    [JsonPropertyName("last_delivery_error")]
    public string? LastDeliveryError { get; init; }

    /// <summary>
    ///     The delivery target(s) — a single token or comma-separated fan-out list (e.g.
    ///     <c>telegram,discord</c>, <c>origin,all</c>). See <see cref="HermesAgentDeliverModes" /> for the REST
    ///     default (<c>local</c>).
    /// </summary>
    [JsonPropertyName("deliver")]
    public string? Deliver { get; init; }

    /// <summary>
    ///     The creating chat's origin, attached by the server (clients cannot supply it). Used as the target of
    ///     <c>origin</c> delivery.
    /// </summary>
    [JsonPropertyName("origin")]
    public HermesAgentJobOrigin? Origin { get; init; }

    /// <summary>The toolsets the agent is restricted to for this job's runs, if any. Not settable over the REST surface.</summary>
    [JsonPropertyName("enabled_toolsets")]
    public IReadOnlyList<string>? EnabledToolsets { get; init; }

    /// <summary>The absolute working directory the job runs inside, if any. Not settable over the REST surface.</summary>
    [JsonPropertyName("workdir")]
    public string? Workdir { get; init; }

    /// <summary>
    ///     Whether run output is mirrored into a session. Only present when explicitly set on the job; when
    ///     absent the server's global <c>cron.mirror_delivery</c> configuration applies.
    /// </summary>
    [JsonPropertyName("attach_to_session")]
    public bool? AttachToSession { get; init; }
}