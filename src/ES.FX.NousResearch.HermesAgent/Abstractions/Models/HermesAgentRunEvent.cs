using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     Base type of the events streamed by <c>GET /v1/runs/{run_id}/events</c>. The feed is data-only SSE:
///     every event is a plain <c>data:</c> payload whose <c>event</c> key identifies the type (there is no SSE
///     <c>event:</c> field, unlike the chat and responses streams). Pattern-match on the sealed derived
///     records; payloads the client does not recognize are surfaced as
///     <see cref="HermesAgentRunUnknownEvent" /> so new server event types never fail the stream.
/// </summary>
public abstract record HermesAgentRunEvent
{
    /// <summary>The event name carried by the payload (e.g. <c>message.delta</c>, <c>run.completed</c>).</summary>
    [JsonPropertyName("event")]
    public string? Event { get; init; }

    /// <summary>The identifier of the run the event belongs to.</summary>
    [JsonPropertyName("run_id")]
    public string? RunId { get; init; }

    /// <summary>When the event was emitted, as float unix seconds.</summary>
    [JsonPropertyName("timestamp")]
    public double? Timestamp { get; init; }
}

/// <summary>A <c>message.delta</c> event — an incremental piece of the assistant's output text.</summary>
public sealed record HermesAgentRunMessageDeltaEvent : HermesAgentRunEvent
{
    /// <summary>The text delta to append to the output accumulated so far.</summary>
    [JsonPropertyName("delta")]
    public string? Delta { get; init; }
}

/// <summary>A <c>tool.started</c> event — the agent began executing a tool.</summary>
public sealed record HermesAgentRunToolStartedEvent : HermesAgentRunEvent
{
    /// <summary>The tool name.</summary>
    [JsonPropertyName("tool")]
    public string? Tool { get; init; }

    /// <summary>A short preview/label of the tool invocation.</summary>
    [JsonPropertyName("preview")]
    public string? Preview { get; init; }
}

/// <summary>A <c>tool.completed</c> event — a tool execution finished.</summary>
public sealed record HermesAgentRunToolCompletedEvent : HermesAgentRunEvent
{
    /// <summary>The tool name.</summary>
    [JsonPropertyName("tool")]
    public string? Tool { get; init; }

    /// <summary>The tool execution duration in seconds (three decimal places).</summary>
    [JsonPropertyName("duration")]
    public double? Duration { get; init; }

    /// <summary>Whether the tool execution ended in an error.</summary>
    [JsonPropertyName("error")]
    public bool? Error { get; init; }
}

/// <summary>A <c>reasoning.available</c> event — intermediate reasoning text became available.</summary>
public sealed record HermesAgentRunReasoningAvailableEvent : HermesAgentRunEvent
{
    /// <summary>The reasoning text.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

/// <summary>
///     An <c>approval.request</c> event — the run is waiting for a human approval decision (status
///     <c>waiting_for_approval</c>). Answer it with
///     <see cref="Abstractions.IHermesAgentRunsApi.ResolveApprovalAsync" />. The payload passes through the
///     server's approval subsystem fields (with <see cref="Command" /> redacted); fields beyond the ones
///     modeled here may be present on the wire and are ignored.
/// </summary>
public sealed record HermesAgentRunApprovalRequestEvent : HermesAgentRunEvent
{
    /// <summary>The (server-redacted) command awaiting approval.</summary>
    [JsonPropertyName("command")]
    public string? Command { get; init; }

    /// <summary>A human-readable description of the action awaiting approval, when provided.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    ///     The valid decision values (<c>once</c>, <c>session</c>, <c>always</c>, <c>deny</c>) to send back via
    ///     <see cref="HermesAgentRunApprovalRequest.Choice" />.
    /// </summary>
    [JsonPropertyName("choices")]
    public IReadOnlyList<string> Choices { get; init; } = [];
}

/// <summary>
///     An <c>approval.responded</c> event — a pending approval was resolved and the run status flipped back to
///     <c>running</c>.
/// </summary>
public sealed record HermesAgentRunApprovalRespondedEvent : HermesAgentRunEvent
{
    /// <summary>The canonical resolved choice (<c>once</c>, <c>session</c>, <c>always</c> or <c>deny</c>).</summary>
    [JsonPropertyName("choice")]
    public string? Choice { get; init; }

    /// <summary>The number of queued approvals that were resolved.</summary>
    [JsonPropertyName("resolved")]
    public int? Resolved { get; init; }
}

/// <summary>A terminal <c>run.completed</c> event — the run finished successfully.</summary>
public sealed record HermesAgentRunCompletedEvent : HermesAgentRunEvent
{
    /// <summary>The final output text.</summary>
    [JsonPropertyName("output")]
    public string? Output { get; init; }

    /// <summary>The token usage of the run.</summary>
    [JsonPropertyName("usage")]
    public HermesAgentUsage? Usage { get; init; }
}

/// <summary>A terminal <c>run.failed</c> event — the run ended in an error.</summary>
public sealed record HermesAgentRunFailedEvent : HermesAgentRunEvent
{
    /// <summary>The (server-redacted) error message.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

/// <summary>A terminal <c>run.cancelled</c> event — the run was cancelled (e.g. after a stop request).</summary>
public sealed record HermesAgentRunCancelledEvent : HermesAgentRunEvent;

/// <summary>
///     The forward-compatibility fallback for run events the client does not recognize (or cannot parse).
///     Carries the raw payload so no information is lost; new server event types surface here instead of
///     failing the stream.
/// </summary>
/// <param name="EventType">
///     The payload's <c>event</c> value when present, otherwise the raw SSE event type (<c>message</c> for the
///     data-only run feed).
/// </param>
/// <param name="Data">The raw JSON payload of the event.</param>
public sealed record HermesAgentRunUnknownEvent(string EventType, string Data) : HermesAgentRunEvent;
