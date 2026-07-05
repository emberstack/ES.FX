using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The basic liveness document returned by <c>GET /v1/health</c> (and its alias <c>GET /health</c>) — the
///     only unauthenticated endpoint of the API server.
/// </summary>
public sealed record HermesAgentHealth
{
    /// <summary>The health status (the server reports <c>ok</c>).</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>The serving platform (the server reports <c>hermes-agent</c>).</summary>
    [JsonPropertyName("platform")]
    public string? Platform { get; init; }

    /// <summary>The server version (a semver string, or <c>dev</c>).</summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }
}