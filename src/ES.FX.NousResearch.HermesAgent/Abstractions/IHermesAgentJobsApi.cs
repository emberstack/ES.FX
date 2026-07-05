using ES.FX.NousResearch.HermesAgent.Abstractions.Models;

namespace ES.FX.NousResearch.HermesAgent.Abstractions;

/// <summary>
///     Operations against the Hermes Agent scheduled jobs (cron) resource (<c>/api/jobs</c>). Job ids are
///     exactly 12 lowercase hex characters — the server rejects any other format with <c>400</c> before
///     touching the job store. Unlike the OpenAI-compatible <c>/v1</c> endpoints, jobs errors use the flat
///     <c>{"error": "..."}</c> shape; both shapes surface as <see cref="HermesAgentApiException" />. The whole
///     surface responds <c>501</c> when the server's cron module is unavailable.
/// </summary>
public interface IHermesAgentJobsApi
{
    /// <summary>
    ///     Lists jobs (<c>GET /api/jobs</c>). The server filters out disabled jobs
    ///     (<see cref="HermesAgentJob.Enabled" /> <c>== false</c>) — which includes paused jobs — so this
    ///     returns only jobs eligible for scheduling; fetch a paused job directly with
    ///     <see cref="GetByIdAsync" />. No pagination and no sort guarantee (storage order).
    /// </summary>
    Task<IReadOnlyList<HermesAgentJob>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a scheduled job (<c>POST /api/jobs</c>; the server responds <c>200</c>, not 201).
    ///     <see cref="HermesAgentJobWrite.Name" /> and <see cref="HermesAgentJobWrite.Schedule" /> are
    ///     required; unknown fields in the body are silently ignored and the server attaches the
    ///     <see cref="HermesAgentJob.Origin" /> itself. Returns the full stored job, with the schedule string
    ///     parsed into a <see cref="HermesAgentJobSchedule" />. QUIRK: an unparseable schedule string fails
    ///     with <c>500</c> (not 400).
    /// </summary>
    Task<HermesAgentJob> CreateAsync(HermesAgentJobWrite job, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a job by id (<c>GET /api/jobs/{job_id}</c>). Note that a repeat-limited job deletes itself
    ///     once its run budget completes, after which this fails with <c>404</c>.
    /// </summary>
    Task<HermesAgentJob> GetByIdAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Partially updates a job (<c>PATCH /api/jobs/{job_id}</c>). Unset (<c>null</c>) properties are
    ///     omitted from the request and left untouched (shallow merge); non-whitelisted fields are silently
    ///     dropped, and a body with no recognized fields fails with <c>400</c>. A supplied
    ///     <see cref="HermesAgentJobWrite.Schedule" /> string is re-parsed exactly like create (invalid →
    ///     <c>500</c>). Avoid setting <see cref="HermesAgentJobWrite.Repeat" /> here — see the footgun note on
    ///     that property. Prefer <see cref="PauseAsync" />/<see cref="ResumeAsync" /> over toggling enablement.
    /// </summary>
    Task<HermesAgentJob> UpdateAsync(string jobId, HermesAgentJobWrite job,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a job (<c>DELETE /api/jobs/{job_id}</c>; the server responds <c>200 {"ok": true}</c>, not
    ///     204) and removes the job's local output directory. Irreversible.
    /// </summary>
    Task DeleteAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Pauses a job (<c>POST /api/jobs/{job_id}/pause</c>; no request body): sets
    ///     <c>enabled=false</c>, <c>state="paused"</c> and <c>paused_at</c>. The job keeps its
    ///     <c>next_run_at</c> but is not scheduled, and disappears from <see cref="ListAsync" /> (the server
    ///     hides disabled jobs). Returns the paused job.
    /// </summary>
    Task<HermesAgentJob> PauseAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Resumes a paused job (<c>POST /api/jobs/{job_id}/resume</c>; no request body): sets
    ///     <c>enabled=true</c>, <c>state="scheduled"</c>, clears the pause fields and recomputes
    ///     <c>next_run_at</c> to the next FUTURE occurrence (missed runs are not replayed). Returns the
    ///     resumed job.
    /// </summary>
    Task<HermesAgentJob> ResumeAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Triggers a job (<c>POST /api/jobs/{job_id}/run</c>; no request body) by re-arming it with
    ///     <c>next_run_at = now</c>; the scheduler fires it on its next tick (within ~60 seconds). Also
    ///     un-pauses a paused job as a side effect. Returns the re-armed job, NOT the run result — observe the
    ///     run via <see cref="HermesAgentJob.LastRunAt" /> and <see cref="HermesAgentJob.LastStatus" />.
    /// </summary>
    Task<HermesAgentJob> TriggerAsync(string jobId, CancellationToken cancellationToken = default);
}
