using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     A page of sessions (<c>GET /api/sessions</c>), ordered by effective last activity descending. Archived
///     sessions are always excluded (the API has no knob to include them).
/// </summary>
public sealed record HermesAgentSessionsResult
{
    /// <summary>The object type discriminator (<c>list</c>).</summary>
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    /// <summary>
    ///     The sessions on this page. The list-only fields (<see cref="HermesAgentSession.LastActive" />,
    ///     <see cref="HermesAgentSession.Preview" />, <see cref="HermesAgentSession.LineageRootId" />) are
    ///     populated here and only here.
    /// </summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<HermesAgentSession> Data { get; init; } = [];

    /// <summary>The applied (possibly clamped) page size.</summary>
    [JsonPropertyName("limit")]
    public int Limit { get; init; }

    /// <summary>The applied (possibly clamped) offset.</summary>
    [JsonPropertyName("offset")]
    public int Offset { get; init; }

    /// <summary>
    ///     Whether more sessions may exist after this page. HEURISTIC: the server reports <c>true</c> whenever
    ///     the page is exactly full, so an exactly-full LAST page still reports <c>true</c> — treat it as
    ///     "maybe more", not a guarantee.
    /// </summary>
    [JsonPropertyName("has_more")]
    public bool HasMore { get; init; }
}