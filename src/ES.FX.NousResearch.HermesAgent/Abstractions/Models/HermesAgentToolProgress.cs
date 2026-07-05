using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The payload of a <c>hermes.tool.progress</c> server-sent event: server-side tool activity interleaved
///     with an SSE stream. A <c>running</c> event opens a tool call (carrying <see cref="Emoji" /> and
///     <see cref="Label" />); a matching <c>completed</c> event (same <see cref="ToolCallId" />) closes it.
///     Internal tools and calls without a tool-call id are never emitted by the server.
/// </summary>
public sealed record HermesAgentToolProgress
{
    /// <summary>The tool name.</summary>
    [JsonPropertyName("tool")]
    public string? Tool { get; init; }

    /// <summary>The emoji associated with the tool (<c>running</c> events only).</summary>
    [JsonPropertyName("emoji")]
    public string? Emoji { get; init; }

    /// <summary>The human-readable preview label (<c>running</c> events only).</summary>
    [JsonPropertyName("label")]
    public string? Label { get; init; }

    /// <summary>The tool-call identifier correlating the <c>running</c> and <c>completed</c> events (note the camelCase wire name <c>toolCallId</c>).</summary>
    [JsonPropertyName("toolCallId")]
    public string? ToolCallId { get; init; }

    /// <summary>The tool-call status (<c>running</c> or <c>completed</c>).</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}
