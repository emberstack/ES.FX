using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The Hermes <c>hermes</c> extension object (not part of the OpenAI schema). Carried as a top-level member
///     of a soft-partial chat completion body and of the final stream chunk when the run did not finish
///     cleanly, describing how the agent run actually ended. A reduced form (<c>completed</c>/<c>partial</c>/
///     <c>failed</c> only) is embedded inside the <c>error.hermes</c> member of a <c>502</c> hard-fail error
///     envelope (available raw via <see cref="HermesAgentApiException.ResponseBody" />).
/// </summary>
public sealed record HermesAgentHermesStatus
{
    /// <summary>Whether the agent run completed.</summary>
    [JsonPropertyName("completed")]
    public bool? Completed { get; init; }

    /// <summary>Whether the output is partial (some text was produced before the run degraded).</summary>
    [JsonPropertyName("partial")]
    public bool? Partial { get; init; }

    /// <summary>Whether the agent run failed.</summary>
    [JsonPropertyName("failed")]
    public bool? Failed { get; init; }

    /// <summary>The redacted error message, if any.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>The error code (<c>output_truncated</c> or <c>agent_error</c>), if any.</summary>
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; init; }
}