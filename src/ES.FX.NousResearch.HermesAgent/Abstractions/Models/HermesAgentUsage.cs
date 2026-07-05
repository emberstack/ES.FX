using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     Token usage in the OpenAI Responses convention (<c>input_tokens</c> / <c>output_tokens</c> /
///     <c>total_tokens</c>), reported by the Responses API, by run statuses and run events, and by session chat.
///     The chat-completions endpoint reports usage in the <c>prompt_tokens</c> convention instead and has its own
///     usage model.
/// </summary>
public sealed record HermesAgentUsage
{
    /// <summary>The number of input (prompt) tokens consumed.</summary>
    [JsonPropertyName("input_tokens")]
    public int? InputTokens { get; init; }

    /// <summary>The number of output (completion) tokens produced.</summary>
    [JsonPropertyName("output_tokens")]
    public int? OutputTokens { get; init; }

    /// <summary>The total number of tokens (input plus output).</summary>
    [JsonPropertyName("total_tokens")]
    public int? TotalTokens { get; init; }
}
