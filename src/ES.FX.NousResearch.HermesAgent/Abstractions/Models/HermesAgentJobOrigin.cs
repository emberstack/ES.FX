using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The origin of a job — attached by the server at create time (clients cannot supply it) and used as the
///     target of <c>origin</c> delivery. For jobs created over this REST surface the platform is always
///     <c>api_server</c>; the network fields are best-effort audit data and may be absent.
/// </summary>
public sealed record HermesAgentJobOrigin
{
    /// <summary>The originating platform (e.g. <c>api_server</c>).</summary>
    [JsonPropertyName("platform")]
    public string? Platform { get; init; }

    /// <summary>The originating chat id (<c>api</c> for REST-created jobs).</summary>
    [JsonPropertyName("chat_id")]
    public string? ChatId { get; init; }

    /// <summary>The source IP of the creating request, when known.</summary>
    [JsonPropertyName("source_ip")]
    public string? SourceIp { get; init; }

    /// <summary>The peer IP of the creating request, when known.</summary>
    [JsonPropertyName("peer_ip")]
    public string? PeerIp { get; init; }

    /// <summary>The creating request's <c>X-Forwarded-For</c> value, when present.</summary>
    [JsonPropertyName("forwarded_for")]
    public string? ForwardedFor { get; init; }

    /// <summary>The creating request's <c>X-Real-IP</c> value, when present.</summary>
    [JsonPropertyName("real_ip")]
    public string? RealIp { get; init; }

    /// <summary>The creating request's <c>User-Agent</c> value, when present.</summary>
    [JsonPropertyName("user_agent")]
    public string? UserAgent { get; init; }
}
