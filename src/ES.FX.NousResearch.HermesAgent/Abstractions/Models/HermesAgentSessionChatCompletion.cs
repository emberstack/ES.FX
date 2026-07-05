using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The result of a synchronous session chat turn (<c>POST /api/sessions/{session_id}/chat</c>).
/// </summary>
public sealed record HermesAgentSessionChatCompletion
{
    /// <summary>The object type discriminator (<c>hermes.session.chat.completion</c>).</summary>
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    /// <summary>
    ///     The EFFECTIVE session id the turn ran on. May differ from the requested id when the agent rotated
    ///     sessions mid-turn (e.g. context compression) — use this id for follow-up requests.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string SessionId { get; init; } = string.Empty;

    /// <summary>The assistant's reply.</summary>
    [JsonPropertyName("message")]
    public HermesAgentSessionChatMessage? Message { get; init; }

    /// <summary>Cumulative agent-session token usage counters (NOT per-turn deltas).</summary>
    [JsonPropertyName("usage")]
    public HermesAgentUsage? Usage { get; init; }
}

/// <summary>The assistant reply message of a session chat turn.</summary>
public sealed record HermesAgentSessionChatMessage
{
    /// <summary>The message role (<c>assistant</c>).</summary>
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    /// <summary>
    ///     The final reply text. <c>MEDIA:</c> tags referencing local images are inlined by the server as
    ///     <c>![image](data:...;base64,...)</c> markdown.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }
}