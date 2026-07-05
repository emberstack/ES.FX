using System.Text.Json;
using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The detailed runtime health document returned by <c>GET /health/detailed</c> (authenticated; note the
///     path is NOT under <c>/v1</c>). Most fields come from the server's runtime status file — when that file is
///     missing, <see cref="GatewayState" /> is <c>null</c>, <see cref="Platforms" /> is empty and the boolean
///     flags are <c>false</c>; <see cref="Pid" /> is always the live process id.
/// </summary>
public sealed record HermesAgentDetailedHealth
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

    /// <summary>The gateway state, or <c>null</c> when the runtime status file is missing.</summary>
    [JsonPropertyName("gateway_state")]
    public string? GatewayState { get; init; }

    /// <summary>
    ///     The per-platform runtime status, keyed by platform name. The value shape is server-defined (an open
    ///     object), so entries are surfaced as raw <see cref="JsonElement" /> documents.
    /// </summary>
    [JsonPropertyName("platforms")]
    public IReadOnlyDictionary<string, JsonElement>? Platforms { get; init; }

    /// <summary>The number of active agents.</summary>
    [JsonPropertyName("active_agents")]
    public int? ActiveAgents { get; init; }

    /// <summary>Whether the gateway is currently busy.</summary>
    [JsonPropertyName("gateway_busy")]
    public bool? GatewayBusy { get; init; }

    /// <summary>Whether the gateway can currently be drained.</summary>
    [JsonPropertyName("gateway_drainable")]
    public bool? GatewayDrainable { get; init; }

    /// <summary>The reason the gateway exited, if it has.</summary>
    [JsonPropertyName("exit_reason")]
    public string? ExitReason { get; init; }

    /// <summary>When the runtime status was last updated (a server-formatted string), if known.</summary>
    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }

    /// <summary>The live process id of the API server.</summary>
    [JsonPropertyName("pid")]
    public int? Pid { get; init; }
}