using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     An output item of a Responses API turn. The item kind is discriminated by <see cref="Type" />
///     (<c>message</c>, <c>function_call</c> or <c>function_call_output</c>) and kept string-typed so unknown
///     future kinds never break deserialization; only the fields relevant to the kind are populated.
///     <see cref="Id" /> and <see cref="Status" /> appear only on items emitted by the streaming
///     <c>response.output_item.added</c>/<c>done</c> events — the terminal and non-streaming envelopes omit them.
/// </summary>
public sealed record HermesAgentResponseOutputItem
{
    /// <summary>The item type (<c>message</c>, <c>function_call</c> or <c>function_call_output</c>).</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    ///     The item identifier (<c>msg_…</c>, <c>fc_…</c> or <c>fco_…</c>) — present on streaming item events
    ///     only.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The item status (<c>in_progress</c> or <c>completed</c>) — present on streaming item events only.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>The message role (<c>assistant</c>) — <c>message</c> items only.</summary>
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    /// <summary>The message content parts (<c>output_text</c>) — <c>message</c> items only.</summary>
    [JsonPropertyName("content")]
    public IReadOnlyList<HermesAgentResponseContentPart>? Content { get; init; }

    /// <summary>The tool name — <c>function_call</c> items only.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    ///     The tool arguments as a JSON string — <c>function_call</c> items only. In the terminal streaming
    ///     envelope, long argument string values may be replaced by a truncation placeholder.
    /// </summary>
    [JsonPropertyName("arguments")]
    public string? Arguments { get; init; }

    /// <summary>The tool call id linking a <c>function_call</c> to its <c>function_call_output</c>.</summary>
    [JsonPropertyName("call_id")]
    public string? CallId { get; init; }

    /// <summary>
    ///     The tool result — <c>function_call_output</c> items only. A plain string on the non-streaming path,
    ///     a part array in streaming terminal payloads (see <see cref="HermesAgentResponseFunctionCallOutput" />).
    /// </summary>
    [JsonPropertyName("output")]
    public HermesAgentResponseFunctionCallOutput? Output { get; init; }
}