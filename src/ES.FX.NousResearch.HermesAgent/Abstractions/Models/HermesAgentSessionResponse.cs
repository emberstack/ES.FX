using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The single-session envelope (<c>{"object": "hermes.session", "session": {...}}</c>) returned by the
///     session create/get/update/fork endpoints. Unwrapped by the sessions area implementation.
/// </summary>
internal sealed record HermesAgentSessionResponse
{
    /// <summary>The object type discriminator (<c>hermes.session</c>).</summary>
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    /// <summary>The session payload.</summary>
    [JsonPropertyName("session")]
    public HermesAgentSession? Session { get; init; }
}
