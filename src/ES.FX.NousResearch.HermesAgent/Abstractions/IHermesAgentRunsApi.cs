using ES.FX.NousResearch.HermesAgent.Abstractions.Models;

namespace ES.FX.NousResearch.HermesAgent.Abstractions;

/// <summary>
///     Operations against the Hermes Agent <c>/v1/runs</c> endpoints — structured asynchronous runs with a
///     pollable status object, a one-shot server-sent event feed, cooperative stop and human-in-the-loop
///     approval resolution. Run state lives in server memory only (lost on restart): an event stream that is
///     never consumed is swept after ~300 seconds, and terminal statuses remain pollable for ~3600 seconds
///     after the last update before the run starts returning <c>404</c>.
/// </summary>
public interface IHermesAgentRunsApi
{
    /// <summary>
    ///     Submits a new asynchronous run (<c>POST /v1/runs</c>; returns <c>202 Accepted</c> with the new run id
    ///     and the literal acknowledgment status <c>started</c>, which never appears as a run
    ///     <c>status</c> value — see <see cref="HermesAgentRunStatuses" /> for the pollable lifecycle). The
    ///     server enforces a shared concurrency cap and responds <c>429</c> with a <c>Retry-After</c> header when
    ///     it is exceeded. Of the optional per-call headers only
    ///     <see cref="HermesAgentRequestHeaders.SessionKey" /> is honored by this endpoint; the run's Hermes
    ///     session id is set via <see cref="HermesAgentRunRequest.SessionId" /> instead, and idempotency keys are
    ///     not supported for runs.
    /// </summary>
    Task<HermesAgentRunCreated> CreateAsync(HermesAgentRunRequest request,
        HermesAgentRequestHeaders? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the pollable status object of a run (<c>GET /v1/runs/{run_id}</c>). Fields accumulate as the
    ///     run progresses and are absent until first set (e.g. <c>output</c> appears only once completed,
    ///     <c>error</c> only once failed). Unknown or expired runs return <c>404</c> with error code
    ///     <c>run_not_found</c>.
    /// </summary>
    Task<HermesAgentRun> GetByIdAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Streams the live event feed of a run (<c>GET /v1/runs/{run_id}/events</c>; server-sent events).
    ///     The feed is one-shot: the underlying queue is deleted when the stream ends, so there is no
    ///     replay and no second subscription (a later call returns <c>404</c>). The server waits up to ~1 second
    ///     for a freshly submitted run to register before responding <c>404</c> with code <c>run_not_found</c>.
    ///     Events are mapped onto the <see cref="HermesAgentRunEvent" /> hierarchy; event payloads the client
    ///     does not recognize are surfaced as <see cref="HermesAgentRunUnknownEvent" /> instead of failing the
    ///     stream. Nothing is sent until the returned sequence is enumerated.
    /// </summary>
    IAsyncEnumerable<HermesAgentRunEvent> StreamEventsAsync(string runId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Requests a cooperative stop of a run (<c>POST /v1/runs/{run_id}/stop</c>). Returns the acknowledgment
    ///     (<c>status</c> = <c>stopping</c>) even when the server-side interrupt itself raised; a stopped run
    ///     typically ends <c>cancelled</c> (or <c>failed</c> when the interrupt surfaced as an error). Runs
    ///     without an active agent or task — including runs that already completed — return <c>404</c> with code
    ///     <c>run_not_found</c>, while status polling for the same run may still succeed.
    /// </summary>
    Task<HermesAgentRunStopped> StopAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Resolves a pending tool approval of a run (<c>POST /v1/runs/{run_id}/approval</c>), answering a
    ///     <see cref="HermesAgentRunApprovalRequestEvent" /> and flipping the run status back to
    ///     <c>running</c>. Approval scope is strictly per run, even when runs share a session id. Conflicts
    ///     return <c>409</c>: code <c>approval_not_active</c> when the run has no live approval session (e.g. it
    ///     already finished), or <c>approval_not_pending</c> when nothing is queued (<c>resolved</c> stays
    ///     <c>0</c>).
    /// </summary>
    Task<HermesAgentRunApprovalResult> ResolveApprovalAsync(string runId, HermesAgentRunApprovalRequest request,
        CancellationToken cancellationToken = default);
}
