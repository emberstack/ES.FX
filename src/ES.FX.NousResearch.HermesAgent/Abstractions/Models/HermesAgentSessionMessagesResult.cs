using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The message history of a session (<c>GET /api/sessions/{session_id}/messages</c>), in insertion order.
///     This endpoint has no pagination.
/// </summary>
public sealed record HermesAgentSessionMessagesResult
{
    /// <summary>The object type discriminator (<c>list</c>).</summary>
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    /// <summary>
    ///     The RESOLVED session id that actually holds the live messages. May differ from the requested id:
    ///     compressed sessions resolve through their continuation lineage to the live tip — follow this id,
    ///     not the one you asked for.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string SessionId { get; init; } = string.Empty;

    /// <summary>The active messages, in insertion order (rewound/soft-deleted rows are excluded).</summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<HermesAgentSessionMessage> Data { get; init; } = [];
}