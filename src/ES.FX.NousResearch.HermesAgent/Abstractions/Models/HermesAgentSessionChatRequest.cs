using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     A session chat turn request (<c>POST /api/sessions/{session_id}/chat</c> and
///     <c>POST /api/sessions/{session_id}/chat/stream</c>). Conversation history is loaded server-side from
///     the session — only the new user message (and an optional ephemeral system message) is sent. The
///     endpoint reads no other fields (no <c>model</c>, <c>stream</c> or sampling parameters).
/// </summary>
public sealed record HermesAgentSessionChatRequest
{
    /// <summary>
    ///     The user message (<c>message</c>): plain text or multimodal content parts — implicitly convertible
    ///     from <see cref="string" />; use <see cref="HermesAgentMessageContent.FromParts" /> with
    ///     <see cref="HermesAgentMessageContentPart" /> items for multimodal input. Effectively required: a
    ///     missing/empty/payload-less message is rejected with <c>400 missing_message</c>. Text is capped at
    ///     65,536 characters per part and part arrays at 1,000 items (silently truncated server-side).
    /// </summary>
    [JsonPropertyName("message")]
    public HermesAgentMessageContent? Message { get; init; }

    /// <summary>
    ///     An ephemeral system prompt (<c>system_message</c>) layered ON TOP of the agent's core prompt for
    ///     this turn only. Not stored on the session.
    /// </summary>
    [JsonPropertyName("system_message")]
    public string? SystemMessage { get; init; }
}
