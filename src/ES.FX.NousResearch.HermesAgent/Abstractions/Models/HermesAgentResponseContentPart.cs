using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     A content part of a Responses API output item. Assistant <c>message</c> items carry
///     <c>output_text</c> parts; the streaming form of a <c>function_call_output</c> item carries
///     <c>input_text</c> parts.
/// </summary>
public sealed record HermesAgentResponseContentPart
{
    /// <summary>The part type (<c>output_text</c> or <c>input_text</c>).</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The text content of the part.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}