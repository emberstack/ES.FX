using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     A Responses API response envelope (<c>POST /v1/responses</c>, <c>GET /v1/responses/{id}</c>, and the
///     <c>response</c> payload of the streaming lifecycle events). Non-streaming creates always report
///     <see cref="Status" /> <c>completed</c> — an agent-produced error string is placed inside the final message
///     text instead; other statuses appear on stored/streamed snapshots.
/// </summary>
public sealed record HermesAgentResponse
{
    /// <summary>The response identifier (<c>resp_…</c>).</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The object type discriminator (always <c>response</c>).</summary>
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    /// <summary>The response status — see <see cref="HermesAgentResponseStatuses" />.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>The creation time in unix seconds.</summary>
    [JsonPropertyName("created_at")]
    public long? CreatedAt { get; init; }

    /// <summary>The model name echoed from the request (or the server's advertised model).</summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>
    ///     The output items of the current turn, in order: <c>function_call</c> / <c>function_call_output</c>
    ///     pairs per tool invocation, then the final assistant <c>message</c>.
    /// </summary>
    [JsonPropertyName("output")]
    public IReadOnlyList<HermesAgentResponseOutputItem> Output { get; init; } = [];

    /// <summary>The token usage of the turn.</summary>
    [JsonPropertyName("usage")]
    public HermesAgentUsage? Usage { get; init; }

    /// <summary>The error details — present only on <c>failed</c> snapshots (e.g. the terminal streaming event).</summary>
    [JsonPropertyName("error")]
    public HermesAgentError? Error { get; init; }

    /// <summary>
    ///     The effective Hermes session id, read from the <c>X-Hermes-Session-Id</c> response header of
    ///     <c>POST /v1/responses</c> (not part of the JSON body). Populated only on envelopes returned by
    ///     <see cref="Abstractions.IHermesAgentResponsesApi.CreateAsync" /> — <c>null</c> on stored envelopes
    ///     from <see cref="Abstractions.IHermesAgentResponsesApi.GetByIdAsync" /> and on the snapshots carried
    ///     inside streaming events (the streaming path surfaces the id via
    ///     <see cref="HermesAgentResponseStreamStartEvent" /> instead).
    /// </summary>
    [JsonIgnore]
    public string? EffectiveSessionId { get; init; }
}
