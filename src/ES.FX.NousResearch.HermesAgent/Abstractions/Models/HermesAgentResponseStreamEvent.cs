using System.Text.Json;
using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     Base type for the typed server-sent events of a streaming Responses API request
///     (<c>POST /v1/responses</c> with <c>stream: true</c>). Every event payload carries a monotonically
///     increasing <c>sequence_number</c> (starting at 0 across the stream). Event types the client does not
///     recognize are surfaced as <see cref="HermesAgentResponseUnknownEvent" /> — they never fail the stream.
/// </summary>
public abstract record HermesAgentResponseStreamEvent;

/// <summary>
///     A client-synthesized event yielded FIRST — before any server event — when the server reported the
///     effective Hermes session id on the <c>X-Hermes-Session-Id</c> response header of the stream. It is not
///     part of the wire protocol: the header is the only place the effective (derived or rotated) session id
///     appears on a streaming response, and this event is how the client surfaces it.
/// </summary>
/// <param name="EffectiveSessionId">
///     The effective session id — send it as <see cref="HermesAgentRequestHeaders.SessionId" /> on a
///     chat-completions turn (Responses API continuity itself uses <c>previous_response_id</c> /
///     <c>conversation</c>) or use it against the sessions surface.
/// </param>
public sealed record HermesAgentResponseStreamStartEvent(string EffectiveSessionId) : HermesAgentResponseStreamEvent;

/// <summary>
///     The <c>response.created</c> event — the first event of the stream, carrying the in-progress response
///     envelope (empty <c>output</c>).
/// </summary>
public sealed record HermesAgentResponseCreatedEvent : HermesAgentResponseStreamEvent
{
    /// <summary>The in-progress response envelope (status <c>in_progress</c>).</summary>
    [JsonPropertyName("response")]
    public HermesAgentResponse? Response { get; init; }

    /// <summary>The position of the event in the stream.</summary>
    [JsonPropertyName("sequence_number")]
    public long? SequenceNumber { get; init; }
}

/// <summary>
///     The <c>response.output_item.added</c> event — a new output item (assistant message, tool call or tool
///     result) opened at <see cref="OutputIndex" />.
/// </summary>
public sealed record HermesAgentResponseOutputItemAddedEvent : HermesAgentResponseStreamEvent
{
    /// <summary>The index of the item in the response output.</summary>
    [JsonPropertyName("output_index")]
    public int? OutputIndex { get; init; }

    /// <summary>The added item (status <c>in_progress</c>).</summary>
    [JsonPropertyName("item")]
    public HermesAgentResponseOutputItem? Item { get; init; }

    /// <summary>The position of the event in the stream.</summary>
    [JsonPropertyName("sequence_number")]
    public long? SequenceNumber { get; init; }
}

/// <summary>
///     The <c>response.output_text.delta</c> event — an incremental text delta of the assistant message
///     (deltas are batched roughly every 50 ms server-side).
/// </summary>
public sealed record HermesAgentResponseOutputTextDeltaEvent : HermesAgentResponseStreamEvent
{
    /// <summary>The id of the assistant message item the delta belongs to (<c>msg_…</c>).</summary>
    [JsonPropertyName("item_id")]
    public string? ItemId { get; init; }

    /// <summary>The index of the item in the response output.</summary>
    [JsonPropertyName("output_index")]
    public int? OutputIndex { get; init; }

    /// <summary>The index of the content part within the item (always <c>0</c>).</summary>
    [JsonPropertyName("content_index")]
    public int? ContentIndex { get; init; }

    /// <summary>The text delta.</summary>
    [JsonPropertyName("delta")]
    public string? Delta { get; init; }

    /// <summary>The log probabilities (the server always sends an empty array).</summary>
    [JsonPropertyName("logprobs")]
    public IReadOnlyList<JsonElement> Logprobs { get; init; } = [];

    /// <summary>The position of the event in the stream.</summary>
    [JsonPropertyName("sequence_number")]
    public long? SequenceNumber { get; init; }
}

/// <summary>
///     The <c>response.output_text.done</c> event — the full assembled text of the assistant message (emitted
///     only when text was streamed).
/// </summary>
public sealed record HermesAgentResponseOutputTextDoneEvent : HermesAgentResponseStreamEvent
{
    /// <summary>The id of the assistant message item the text belongs to (<c>msg_…</c>).</summary>
    [JsonPropertyName("item_id")]
    public string? ItemId { get; init; }

    /// <summary>The index of the item in the response output.</summary>
    [JsonPropertyName("output_index")]
    public int? OutputIndex { get; init; }

    /// <summary>The index of the content part within the item (always <c>0</c>).</summary>
    [JsonPropertyName("content_index")]
    public int? ContentIndex { get; init; }

    /// <summary>The full text of the assistant message.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>The log probabilities (the server always sends an empty array).</summary>
    [JsonPropertyName("logprobs")]
    public IReadOnlyList<JsonElement> Logprobs { get; init; } = [];

    /// <summary>The position of the event in the stream.</summary>
    [JsonPropertyName("sequence_number")]
    public long? SequenceNumber { get; init; }
}

/// <summary>
///     The <c>response.output_item.done</c> event — an output item finalized (status <c>completed</c>).
/// </summary>
public sealed record HermesAgentResponseOutputItemDoneEvent : HermesAgentResponseStreamEvent
{
    /// <summary>The index of the item in the response output.</summary>
    [JsonPropertyName("output_index")]
    public int? OutputIndex { get; init; }

    /// <summary>The finalized item.</summary>
    [JsonPropertyName("item")]
    public HermesAgentResponseOutputItem? Item { get; init; }

    /// <summary>The position of the event in the stream.</summary>
    [JsonPropertyName("sequence_number")]
    public long? SequenceNumber { get; init; }
}

/// <summary>
///     The <c>response.completed</c> terminal event — the finished response envelope with the full output and
///     usage. Large tool argument/result payloads inside <c>output</c> are truncated server-side.
/// </summary>
public sealed record HermesAgentResponseCompletedEvent : HermesAgentResponseStreamEvent
{
    /// <summary>The completed response envelope (status <c>completed</c>).</summary>
    [JsonPropertyName("response")]
    public HermesAgentResponse? Response { get; init; }

    /// <summary>The position of the event in the stream.</summary>
    [JsonPropertyName("sequence_number")]
    public long? SequenceNumber { get; init; }
}

/// <summary>
///     The <c>response.failed</c> terminal event — the failed response envelope, carrying the error details on
///     <see cref="HermesAgentResponse.Error" />.
/// </summary>
public sealed record HermesAgentResponseFailedEvent : HermesAgentResponseStreamEvent
{
    /// <summary>The failed response envelope (status <c>failed</c>).</summary>
    [JsonPropertyName("response")]
    public HermesAgentResponse? Response { get; init; }

    /// <summary>The position of the event in the stream.</summary>
    [JsonPropertyName("sequence_number")]
    public long? SequenceNumber { get; init; }
}

/// <summary>
///     Fallback for a server-sent event the client does not recognize (or whose payload no longer parses).
///     Carries the raw event name and payload so new server event types never break consumers.
/// </summary>
/// <param name="EventType">The SSE event name (the <c>event:</c> field).</param>
/// <param name="Data">The raw event payload (the <c>data:</c> field, typically JSON).</param>
public sealed record HermesAgentResponseUnknownEvent(string EventType, string Data) : HermesAgentResponseStreamEvent;
