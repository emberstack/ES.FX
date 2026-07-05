using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The error object carried by a non-success Hermes Agent API response. The OpenAI-compatible endpoints
///     use the envelope <c>{"error": {"message", "type", "param", "code"}}</c>; the jobs endpoints use a flat
///     <c>{"error": "&lt;string&gt;"}</c> shape, which is mapped to this record with only
///     <see cref="Message" /> set.
/// </summary>
public sealed record HermesAgentError
{
    /// <summary>The human-readable error message (server-side secrets are redacted by the server).</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>The error type (e.g. <c>invalid_request_error</c>, <c>server_error</c>, <c>rate_limit_error</c>).</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The request parameter the error refers to (e.g. <c>messages[0].content</c>), if any.</summary>
    [JsonPropertyName("param")]
    public string? Param { get; init; }

    /// <summary>The machine-readable error code (e.g. <c>invalid_api_key</c>, <c>rate_limit_exceeded</c>), if any.</summary>
    [JsonPropertyName("code")]
    public string? Code { get; init; }
}
