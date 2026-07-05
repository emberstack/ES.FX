using System.Text.Json;
using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     Base type for the typed events of the session chat stream
///     (<c>POST /api/sessions/{session_id}/chat/stream</c>). The server injects the four common fields below
///     into every event payload. Events arrive in lifecycle order: <c>run.started</c>, <c>message.started</c>,
///     zero or more of <c>assistant.delta</c> / <c>tool.progress</c> / <c>tool.started</c> /
///     <c>tool.completed</c> / <c>tool.failed</c>, then <c>assistant.completed</c> and <c>run.completed</c>
///     (or <c>error</c> on failure), and always a final <c>done</c>. Unrecognized event types surface as
///     <see cref="HermesAgentSessionChatUnknownEvent" /> — never as an exception.
/// </summary>
public abstract record HermesAgentSessionChatEvent
{
    /// <summary>
    ///     The session id. This is the requested path id on most events; on <c>assistant.completed</c> and
    ///     <c>run.completed</c> it is the EFFECTIVE (possibly rotated) session id.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    /// <summary>The run id for this turn (<c>run_{32 hex}</c>; one per request).</summary>
    [JsonPropertyName("run_id")]
    public string? RunId { get; init; }

    /// <summary>The 1-based sequence number, incrementing per event in enqueue order.</summary>
    [JsonPropertyName("seq")]
    public long? Seq { get; init; }

    /// <summary>When the event was enqueued (Unix epoch seconds).</summary>
    [JsonPropertyName("ts")]
    public double? Ts { get; init; }
}

/// <summary>The <c>run.started</c> event: the turn was accepted and the normalized user message is echoed back.</summary>
public sealed record HermesAgentSessionChatRunStartedEvent : HermesAgentSessionChatEvent
{
    /// <summary>
    ///     The normalized user message. Its <c>content</c> is a string, or a content-part array for multimodal
    ///     input.
    /// </summary>
    [JsonPropertyName("user_message")]
    public HermesAgentSessionMessage? UserMessage { get; init; }
}

/// <summary>The <c>message.started</c> event: the assistant message for this turn was allocated.</summary>
public sealed record HermesAgentSessionChatMessageStartedEvent : HermesAgentSessionChatEvent
{
    /// <summary>The assistant message stub. Its id is shared by all subsequent events of the turn.</summary>
    [JsonPropertyName("message")]
    public HermesAgentSessionChatMessageStart? Message { get; init; }
}

/// <summary>The assistant message stub announced by <see cref="HermesAgentSessionChatMessageStartedEvent" />.</summary>
public sealed record HermesAgentSessionChatMessageStart
{
    /// <summary>The assistant message id (<c>msg_{32 hex}</c>), shared by all events of the turn.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The message role (<c>assistant</c>).</summary>
    [JsonPropertyName("role")]
    public string? Role { get; init; }
}

/// <summary>The <c>assistant.delta</c> event: an incremental piece of the assistant's reply text.</summary>
public sealed record HermesAgentSessionChatAssistantDeltaEvent : HermesAgentSessionChatEvent
{
    /// <summary>The turn's assistant message id.</summary>
    [JsonPropertyName("message_id")]
    public string? MessageId { get; init; }

    /// <summary>The text delta (never empty — empty deltas are dropped server-side).</summary>
    [JsonPropertyName("delta")]
    public string? Delta { get; init; }
}

/// <summary>
///     The <c>tool.progress</c> event: reasoning or preview text emitted while a tool (or the model's
///     thinking) runs.
/// </summary>
public sealed record HermesAgentSessionChatToolProgressEvent : HermesAgentSessionChatEvent
{
    /// <summary>The turn's assistant message id.</summary>
    [JsonPropertyName("message_id")]
    public string? MessageId { get; init; }

    /// <summary>
    ///     The tool name — the literal <c>_thinking</c> when the progress comes from the model's reasoning
    ///     rather than a tool.
    /// </summary>
    [JsonPropertyName("tool_name")]
    public string? ToolName { get; init; }

    /// <summary>The progress text delta (may be empty).</summary>
    [JsonPropertyName("delta")]
    public string? Delta { get; init; }
}

/// <summary>
///     Base type for the tool lifecycle events (<c>tool.started</c>, <c>tool.completed</c>,
///     <c>tool.failed</c>), which share a single payload shape.
/// </summary>
public abstract record HermesAgentSessionChatToolEvent : HermesAgentSessionChatEvent
{
    /// <summary>The turn's assistant message id.</summary>
    [JsonPropertyName("message_id")]
    public string? MessageId { get; init; }

    /// <summary>The tool name, if known.</summary>
    [JsonPropertyName("tool_name")]
    public string? ToolName { get; init; }

    /// <summary>A short human-readable preview of the tool activity, if any.</summary>
    [JsonPropertyName("preview")]
    public string? Preview { get; init; }

    /// <summary>
    ///     The tool arguments as passed by the agent, if any. Exposed as a raw <see cref="JsonElement" />
    ///     because the shape is tool-specific.
    /// </summary>
    [JsonPropertyName("args")]
    public JsonElement? Args { get; init; }
}

/// <summary>The <c>tool.started</c> event: a tool call began.</summary>
public sealed record HermesAgentSessionChatToolStartedEvent : HermesAgentSessionChatToolEvent;

/// <summary>The <c>tool.completed</c> event: a tool call finished successfully.</summary>
public sealed record HermesAgentSessionChatToolCompletedEvent : HermesAgentSessionChatToolEvent;

/// <summary>The <c>tool.failed</c> event: a tool call failed.</summary>
public sealed record HermesAgentSessionChatToolFailedEvent : HermesAgentSessionChatToolEvent;

/// <summary>
///     The <c>assistant.completed</c> event: the assistant's final reply for the turn.
///     <see cref="HermesAgentSessionChatEvent.SessionId" /> is the EFFECTIVE session id on this event.
/// </summary>
public sealed record HermesAgentSessionChatAssistantCompletedEvent : HermesAgentSessionChatEvent
{
    /// <summary>The turn's assistant message id.</summary>
    [JsonPropertyName("message_id")]
    public string? MessageId { get; init; }

    /// <summary>
    ///     The final reply text. <c>MEDIA:</c> tags referencing local images are inlined by the server as
    ///     <c>![image](data:...;base64,...)</c> markdown.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }

    /// <summary>Whether the turn completed normally.</summary>
    [JsonPropertyName("completed")]
    public bool? Completed { get; init; }

    /// <summary>Whether the reply is partial.</summary>
    [JsonPropertyName("partial")]
    public bool? Partial { get; init; }

    /// <summary>Whether the turn was interrupted.</summary>
    [JsonPropertyName("interrupted")]
    public bool? Interrupted { get; init; }
}

/// <summary>
///     The <c>run.completed</c> event: the turn finished. Carries the authoritative per-turn transcript and
///     usage. <see cref="HermesAgentSessionChatEvent.SessionId" /> is the EFFECTIVE session id on this event.
/// </summary>
public sealed record HermesAgentSessionChatRunCompletedEvent : HermesAgentSessionChatEvent
{
    /// <summary>The turn's assistant message id.</summary>
    [JsonPropertyName("message_id")]
    public string? MessageId { get; init; }

    /// <summary>Whether the turn completed normally.</summary>
    [JsonPropertyName("completed")]
    public bool? Completed { get; init; }

    /// <summary>
    ///     The authoritative per-turn transcript: only this turn's <c>assistant</c> and <c>tool</c> messages
    ///     (never the user message), each carrying only the keys the agent produced. Intermediate assistant
    ///     text emitted before tool calls — which delta accumulation cannot reconstruct — is recoverable from
    ///     here without a messages round-trip.
    /// </summary>
    [JsonPropertyName("messages")]
    public IReadOnlyList<HermesAgentSessionMessage> Messages { get; init; } = [];

    /// <summary>Cumulative agent-session token usage counters (NOT per-turn deltas).</summary>
    [JsonPropertyName("usage")]
    public HermesAgentUsage? Usage { get; init; }
}

/// <summary>
///     The <c>error</c> event: the turn failed. Emitted instead of <c>assistant.completed</c> /
///     <c>run.completed</c>; the final <c>done</c> event still follows.
/// </summary>
public sealed record HermesAgentSessionChatErrorEvent : HermesAgentSessionChatEvent
{
    /// <summary>The redacted error message.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

/// <summary>The <c>done</c> event: always the final event of the stream (no fields beyond the common four).</summary>
public sealed record HermesAgentSessionChatDoneEvent : HermesAgentSessionChatEvent;

/// <summary>
///     A session chat stream event this client version does not recognize, surfaced for forward compatibility
///     (new server event types never throw). The common fields are NOT parsed for unknown events — inspect
///     the raw <see cref="Data" /> payload directly.
/// </summary>
/// <param name="EventType">The SSE <c>event:</c> name.</param>
/// <param name="Data">The raw event payload (<c>data:</c>), typically JSON.</param>
public sealed record HermesAgentSessionChatUnknownEvent(string EventType, string Data) : HermesAgentSessionChatEvent;