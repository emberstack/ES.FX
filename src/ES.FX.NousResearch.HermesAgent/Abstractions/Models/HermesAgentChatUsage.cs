using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     Token usage reported by the chat completions endpoint (OpenAI chat convention:
///     <c>prompt_tokens</c>/<c>completion_tokens</c>/<c>total_tokens</c>). Sent on the non-streaming completion
///     and on the final stream chunk. Note the Responses/Runs surfaces use the different
///     <c>input_tokens</c>/<c>output_tokens</c> convention.
/// </summary>
public sealed record HermesAgentChatUsage
{
    /// <summary>The number of prompt tokens.</summary>
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    /// <summary>The number of completion tokens.</summary>
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    /// <summary>The total number of tokens.</summary>
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}