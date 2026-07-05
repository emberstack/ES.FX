using ES.FX.NousResearch.HermesAgent.Abstractions.Models;

namespace ES.FX.NousResearch.HermesAgent.Abstractions;

/// <summary>
///     Operations against the Hermes Agent <c>/api/sessions</c> resource: session CRUD, message history,
///     forking, and session-scoped agent chat (synchronous and streaming). Sessions created through this API
///     get <c>source: "api_server"</c> and are persisted in the server's shared state database.
/// </summary>
public interface IHermesAgentSessionsApi
{
    /// <summary>
    ///     Lists sessions (<c>GET /api/sessions</c>), ordered by effective last activity descending; archived
    ///     sessions are always excluded. Invalid query values never fail — the server silently coerces them to
    ///     defaults. Note that <see cref="HermesAgentSessionsResult.HasMore" /> is a page-full heuristic, not a
    ///     true remaining count.
    /// </summary>
    Task<HermesAgentSessionsResult> ListAsync(HermesAgentSessionsQuery? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates an empty session (<c>POST /api/sessions</c>; <c>201</c>). Pass <c>null</c> for a fully
    ///     server-defaulted session (generated id, server model, no title). A taken id fails with
    ///     <c>409 session_exists</c>; a duplicate/too-long title fails with <c>400 invalid_title</c> and the
    ///     just-created session is rolled back.
    /// </summary>
    Task<HermesAgentSession> CreateAsync(HermesAgentSessionWrite? session = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a session by id (<c>GET /api/sessions/{session_id}</c>) — exactly the row asked for, with
    ///     no lineage/tip resolution and without the list-only fields.
    /// </summary>
    Task<HermesAgentSession> GetByIdAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates a session (<c>PATCH /api/sessions/{session_id}</c>). Only <c>title</c> and
    ///     <c>end_reason</c> are updatable; the first end reason wins — re-ending an already-ended session is
    ///     silently ignored (the call still succeeds with the old value).
    /// </summary>
    Task<HermesAgentSession> UpdateAsync(string sessionId, HermesAgentSessionUpdate update,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a session and ALL its messages (<c>DELETE /api/sessions/{session_id}</c>). Irreversible.
    ///     Delegate-subagent children are cascade-deleted; branch/compression children are orphaned
    ///     (<c>parent_session_id</c> cleared) but remain accessible.
    /// </summary>
    Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a session's message history (<c>GET /api/sessions/{session_id}/messages</c>; no
    ///     pagination), in insertion order. The result's <see cref="HermesAgentSessionMessagesResult.SessionId" />
    ///     may differ from the requested id — compressed sessions resolve to the descendant holding the live
    ///     messages; follow it.
    /// </summary>
    Task<HermesAgentSessionMessagesResult> GetMessagesAsync(string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Forks a session (<c>POST /api/sessions/{session_id}/fork</c>; <c>201</c>): creates a new session
    ///     with the source's model and system prompt, a full copy of its messages, and
    ///     <c>parent_session_id</c> set to the source. NOT read-only — the SOURCE session is ended with
    ///     <c>end_reason: "branched"</c> (no-op if already ended). Pass <c>null</c> for a generated id and a
    ///     lineage-derived title.
    /// </summary>
    Task<HermesAgentSession> ForkAsync(string sessionId, HermesAgentSessionForkRequest? request = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Runs one synchronous agent turn on the session (<c>POST /api/sessions/{session_id}/chat</c>).
    ///     Conversation history is loaded server-side — only the new message is sent. The result's
    ///     <see cref="HermesAgentSessionChatCompletion.SessionId" /> is the EFFECTIVE session id (it may
    ///     differ from the requested id when the agent rotates sessions mid-turn) — use it for follow-ups.
    ///     This endpoint is not subject to the server's concurrent-run cap. Of the optional
    ///     <paramref name="headers" />, <c>X-Hermes-Session-Key</c> scopes long-term memory and is echoed back.
    /// </summary>
    Task<HermesAgentSessionChatCompletion> ChatAsync(string sessionId, HermesAgentSessionChatRequest request,
        HermesAgentRequestHeaders? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Runs one agent turn as a server-sent event stream
    ///     (<c>POST /api/sessions/{session_id}/chat/stream</c>). The request is sent lazily when enumeration
    ///     starts; request-validation failures surface as a <see cref="HermesAgentApiException" /> before any
    ///     event, while mid-turn agent failures arrive in-stream as
    ///     <see cref="HermesAgentSessionChatErrorEvent" />. Events arrive in lifecycle order (see
    ///     <see cref="HermesAgentSessionChatEvent" />) ending with a <see cref="HermesAgentSessionChatDoneEvent" />;
    ///     unrecognized event types are surfaced as <see cref="HermesAgentSessionChatUnknownEvent" />, never
    ///     thrown on. Cancelling the enumeration cancels the agent run server-side.
    /// </summary>
    IAsyncEnumerable<HermesAgentSessionChatEvent> StreamChatAsync(string sessionId,
        HermesAgentSessionChatRequest request, HermesAgentRequestHeaders? headers = null,
        CancellationToken cancellationToken = default);
}