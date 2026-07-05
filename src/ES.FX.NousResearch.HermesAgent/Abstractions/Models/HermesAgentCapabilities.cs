using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The server capability document returned by <c>GET /v1/capabilities</c>: platform identity, advertised
///     model, authentication requirements, runtime mode, feature flags and the endpoint catalog.
/// </summary>
public sealed record HermesAgentCapabilities
{
    /// <summary>The object type discriminator (always <c>hermes.api_server.capabilities</c>).</summary>
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    /// <summary>The serving platform (the server reports <c>hermes-agent</c>).</summary>
    [JsonPropertyName("platform")]
    public string? Platform { get; init; }

    /// <summary>The advertised model name (the default <c>model</c> echoed by the serving endpoints).</summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>The authentication scheme the server expects.</summary>
    [JsonPropertyName("auth")]
    public HermesAgentCapabilitiesAuth? Auth { get; init; }

    /// <summary>The runtime execution mode (where the agent and its tools run).</summary>
    [JsonPropertyName("runtime")]
    public HermesAgentCapabilitiesRuntime? Runtime { get; init; }

    /// <summary>The feature flags reflecting the server's configuration.</summary>
    [JsonPropertyName("features")]
    public HermesAgentCapabilitiesFeatures? Features { get; init; }

    /// <summary>
    ///     The endpoint catalog, keyed by endpoint name (e.g. <c>chat_completions</c>, <c>run_events</c>,
    ///     <c>session_fork</c>).
    /// </summary>
    [JsonPropertyName("endpoints")]
    public IReadOnlyDictionary<string, HermesAgentCapabilitiesEndpoint>? Endpoints { get; init; }
}

/// <summary>The <c>auth</c> object of the capability document.</summary>
public sealed record HermesAgentCapabilitiesAuth
{
    /// <summary>The authentication scheme (the server reports <c>bearer</c>).</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>Whether an API key is configured on the server (and therefore required on requests).</summary>
    [JsonPropertyName("required")]
    public bool? Required { get; init; }
}

/// <summary>The <c>runtime</c> object of the capability document.</summary>
public sealed record HermesAgentCapabilitiesRuntime
{
    /// <summary>The runtime mode (the server reports <c>server_agent</c>).</summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    /// <summary>Where tools execute (the server reports <c>server</c> — tools run on the API-server host).</summary>
    [JsonPropertyName("tool_execution")]
    public string? ToolExecution { get; init; }

    /// <summary>Whether a split-runtime mode (client-side tool execution) is enabled.</summary>
    [JsonPropertyName("split_runtime")]
    public bool? SplitRuntime { get; init; }

    /// <summary>A human-readable description of the runtime mode.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
///     The <c>features</c> object of the capability document. Boolean flags reflect the server's configuration;
///     the two header entries advertise the special request-header names the server honors.
/// </summary>
public sealed record HermesAgentCapabilitiesFeatures
{
    /// <summary>Whether <c>POST /v1/chat/completions</c> is available (<c>chat_completions</c>).</summary>
    [JsonPropertyName("chat_completions")]
    public bool? ChatCompletions { get; init; }

    /// <summary>Whether chat completions support SSE streaming (<c>chat_completions_streaming</c>).</summary>
    [JsonPropertyName("chat_completions_streaming")]
    public bool? ChatCompletionsStreaming { get; init; }

    /// <summary>Whether the Responses API (<c>POST /v1/responses</c>) is available (<c>responses_api</c>).</summary>
    [JsonPropertyName("responses_api")]
    public bool? ResponsesApi { get; init; }

    /// <summary>Whether the Responses API supports SSE streaming (<c>responses_streaming</c>).</summary>
    [JsonPropertyName("responses_streaming")]
    public bool? ResponsesStreaming { get; init; }

    /// <summary>Whether asynchronous run submission (<c>POST /v1/runs</c>) is available (<c>run_submission</c>).</summary>
    [JsonPropertyName("run_submission")]
    public bool? RunSubmission { get; init; }

    /// <summary>Whether run status polling (<c>GET /v1/runs/{run_id}</c>) is available (<c>run_status</c>).</summary>
    [JsonPropertyName("run_status")]
    public bool? RunStatus { get; init; }

    /// <summary>Whether the run event stream (<c>GET /v1/runs/{run_id}/events</c>) is available (<c>run_events_sse</c>).</summary>
    [JsonPropertyName("run_events_sse")]
    public bool? RunEventsSse { get; init; }

    /// <summary>Whether runs can be stopped (<c>POST /v1/runs/{run_id}/stop</c>) (<c>run_stop</c>).</summary>
    [JsonPropertyName("run_stop")]
    public bool? RunStop { get; init; }

    /// <summary>Whether run approvals can be resolved (<c>POST /v1/runs/{run_id}/approval</c>) (<c>run_approval_response</c>).</summary>
    [JsonPropertyName("run_approval_response")]
    public bool? RunApprovalResponse { get; init; }

    /// <summary>Whether tool-progress events are emitted on streams (<c>tool_progress_events</c>).</summary>
    [JsonPropertyName("tool_progress_events")]
    public bool? ToolProgressEvents { get; init; }

    /// <summary>Whether approval events are emitted on run event streams (<c>approval_events</c>).</summary>
    [JsonPropertyName("approval_events")]
    public bool? ApprovalEvents { get; init; }

    /// <summary>Whether the session resource endpoints (<c>/api/sessions</c>) are available (<c>session_resources</c>).</summary>
    [JsonPropertyName("session_resources")]
    public bool? SessionResources { get; init; }

    /// <summary>Whether session chat (<c>POST /api/sessions/{session_id}/chat</c>) is available (<c>session_chat</c>).</summary>
    [JsonPropertyName("session_chat")]
    public bool? SessionChat { get; init; }

    /// <summary>Whether session chat supports SSE streaming (<c>session_chat_streaming</c>).</summary>
    [JsonPropertyName("session_chat_streaming")]
    public bool? SessionChatStreaming { get; init; }

    /// <summary>Whether sessions can be forked (<c>POST /api/sessions/{session_id}/fork</c>) (<c>session_fork</c>).</summary>
    [JsonPropertyName("session_fork")]
    public bool? SessionFork { get; init; }

    /// <summary>Whether an admin configuration read/write API is available (<c>admin_config_rw</c>).</summary>
    [JsonPropertyName("admin_config_rw")]
    public bool? AdminConfigReadWrite { get; init; }

    /// <summary>Whether a jobs administration API is exposed on the <c>/v1</c> surface (<c>jobs_admin</c>).</summary>
    [JsonPropertyName("jobs_admin")]
    public bool? JobsAdmin { get; init; }

    /// <summary>Whether a long-term memory write API is available (<c>memory_write_api</c>).</summary>
    [JsonPropertyName("memory_write_api")]
    public bool? MemoryWriteApi { get; init; }

    /// <summary>Whether the skills listing (<c>GET /v1/skills</c>) is available (<c>skills_api</c>).</summary>
    [JsonPropertyName("skills_api")]
    public bool? SkillsApi { get; init; }

    /// <summary>Whether an audio API is available (<c>audio_api</c>).</summary>
    [JsonPropertyName("audio_api")]
    public bool? AudioApi { get; init; }

    /// <summary>Whether realtime voice is available (<c>realtime_voice</c>).</summary>
    [JsonPropertyName("realtime_voice")]
    public bool? RealtimeVoice { get; init; }

    /// <summary>
    ///     The request-header name used for session continuity (<c>session_continuity_header</c>; the server
    ///     advertises <c>X-Hermes-Session-Id</c> — see <see cref="HermesAgentRequestHeaders.SessionId" />).
    /// </summary>
    [JsonPropertyName("session_continuity_header")]
    public string? SessionContinuityHeader { get; init; }

    /// <summary>
    ///     The request-header name used for long-term memory scoping (<c>session_key_header</c>; the server
    ///     advertises <c>X-Hermes-Session-Key</c> — see <see cref="HermesAgentRequestHeaders.SessionKey" />).
    /// </summary>
    [JsonPropertyName("session_key_header")]
    public string? SessionKeyHeader { get; init; }

    /// <summary>Whether any CORS origins are configured on the server (<c>cors</c>).</summary>
    [JsonPropertyName("cors")]
    public bool? Cors { get; init; }
}

/// <summary>An entry of the capability document's <c>endpoints</c> catalog.</summary>
public sealed record HermesAgentCapabilitiesEndpoint
{
    /// <summary>The HTTP method of the endpoint (e.g. <c>GET</c>, <c>POST</c>).</summary>
    [JsonPropertyName("method")]
    public string? Method { get; init; }

    /// <summary>The endpoint path (e.g. <c>/v1/chat/completions</c>, <c>/api/sessions/{session_id}/fork</c>).</summary>
    [JsonPropertyName("path")]
    public string? Path { get; init; }
}
