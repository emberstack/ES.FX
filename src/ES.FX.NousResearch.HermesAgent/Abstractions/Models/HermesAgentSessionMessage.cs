using System.Text.Json;
using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     A message in a session's history (<c>GET /api/sessions/{session_id}/messages</c>). Also the shape of the
///     per-turn transcript items on the session chat stream's <c>run.completed</c> event and of the echoed user
///     message on <c>run.started</c> — on those, only the keys the agent actually produced are present.
/// </summary>
public sealed record HermesAgentSessionMessage
{
    /// <summary>
    ///     The message row id. Present on messages read from the session history; absent (<c>null</c>) on
    ///     per-turn stream transcript items.
    /// </summary>
    [JsonPropertyName("id")]
    public long? Id { get; init; }

    /// <summary>
    ///     The owning session id (the resolved session — see
    ///     <see cref="HermesAgentSessionMessagesResult.SessionId" />).
    /// </summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    /// <summary>The message role (<c>user</c>, <c>assistant</c> or <c>tool</c>; roles are free-form server-side).</summary>
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    /// <summary>
    ///     The message content: usually a JSON string; multimodal user content comes back as an array of
    ///     content parts; may be <c>null</c>. Exposed as a raw <see cref="JsonElement" /> because the wire
    ///     shape is a union.
    /// </summary>
    [JsonPropertyName("content")]
    public JsonElement? Content { get; init; }

    /// <summary>On <c>tool</c> messages, the id of the tool call being answered.</summary>
    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; init; }

    /// <summary>
    ///     On assistant messages, the tool calls issued (OpenAI <c>tool_calls</c> shape). The server
    ///     substitutes <c>[]</c> when the stored value cannot be decoded.
    /// </summary>
    [JsonPropertyName("tool_calls")]
    public IReadOnlyList<HermesAgentSessionMessageToolCall>? ToolCalls { get; init; }

    /// <summary>On <c>tool</c> messages, the name of the tool that produced the result.</summary>
    [JsonPropertyName("tool_name")]
    public string? ToolName { get; init; }

    /// <summary>When the message was recorded (Unix epoch seconds).</summary>
    [JsonPropertyName("timestamp")]
    public double? Timestamp { get; init; }

    /// <summary>The message token count, if tracked.</summary>
    [JsonPropertyName("token_count")]
    public int? TokenCount { get; init; }

    /// <summary>The finish reason, if any (well-known values: <c>stop</c>, <c>length</c>, <c>error</c>).</summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }

    /// <summary>Reasoning text attached to the message, if any.</summary>
    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; init; }

    /// <summary>Provider-specific reasoning content attached to the message, if any.</summary>
    [JsonPropertyName("reasoning_content")]
    public string? ReasoningContent { get; init; }
}

/// <summary>A tool call issued by an assistant message (OpenAI <c>tool_calls</c> item shape).</summary>
public sealed record HermesAgentSessionMessageToolCall
{
    /// <summary>The tool call id (e.g. <c>call_1</c>).</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The tool call type (<c>function</c>).</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The function invocation details.</summary>
    [JsonPropertyName("function")]
    public HermesAgentSessionMessageToolCallFunction? Function { get; init; }
}

/// <summary>The function payload of a <see cref="HermesAgentSessionMessageToolCall" />.</summary>
public sealed record HermesAgentSessionMessageToolCallFunction
{
    /// <summary>The function (tool) name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The function arguments as a JSON-encoded string.</summary>
    [JsonPropertyName("arguments")]
    public string? Arguments { get; init; }
}