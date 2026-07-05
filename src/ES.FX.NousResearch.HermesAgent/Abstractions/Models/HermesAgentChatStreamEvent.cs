namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     An event yielded by <see cref="Abstractions.IHermesAgentChatApi.StreamAsync" />. The chat-completions
///     stream mixes unnamed OpenAI-style chunks (<see cref="HermesAgentChatCompletionChunkEvent" />) with named
///     <c>hermes.tool.progress</c> events (<see cref="HermesAgentToolProgressEvent" />); anything the client
///     does not recognize is surfaced as <see cref="HermesAgentChatStreamUnknownEvent" /> (never an exception),
///     keeping the stream forward compatible.
/// </summary>
public abstract record HermesAgentChatStreamEvent;

/// <summary>
///     A client-synthesized event yielded FIRST — before any server event — when the server reported the
///     effective Hermes session id on the <c>X-Hermes-Session-Id</c> response header of the stream. It is not
///     part of the wire protocol: the header is the only place the effective (derived or rotated) session id
///     appears on a streaming chat completion, and this event is how the client surfaces it.
/// </summary>
/// <param name="EffectiveSessionId">
///     The effective session id — send it as <see cref="HermesAgentRequestHeaders.SessionId" /> on the next
///     turn to continue the conversation server-side.
/// </param>
public sealed record HermesAgentChatStreamStartEvent(string EffectiveSessionId) : HermesAgentChatStreamEvent;

/// <summary>
///     A standard chat-completion chunk (an unnamed <c>data:</c> event of the stream).
/// </summary>
/// <param name="Chunk">The deserialized chunk payload.</param>
public sealed record HermesAgentChatCompletionChunkEvent(HermesAgentChatCompletionChunk Chunk)
    : HermesAgentChatStreamEvent;

/// <summary>
///     A named <c>hermes.tool.progress</c> event: server-side tool activity interleaved with the content
///     chunks. Not part of the OpenAI chunk schema.
/// </summary>
/// <param name="Progress">The deserialized tool-progress payload.</param>
public sealed record HermesAgentToolProgressEvent(HermesAgentToolProgress Progress)
    : HermesAgentChatStreamEvent;

/// <summary>
///     An event the client does not recognize (an unexpected event name or an undeserializable payload).
///     Carried raw so new server-side event types degrade gracefully instead of failing the stream.
/// </summary>
/// <param name="EventType">The SSE event name (<c>message</c> for unnamed <c>data:</c>-only events).</param>
/// <param name="Data">The raw event payload.</param>
public sealed record HermesAgentChatStreamUnknownEvent(string EventType, string Data)
    : HermesAgentChatStreamEvent;