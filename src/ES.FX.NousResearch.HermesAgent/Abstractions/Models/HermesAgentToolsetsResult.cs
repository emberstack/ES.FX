using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The <c>{"object":"list","platform":"api_server","data":[...]}</c> envelope returned by
///     <c>GET /v1/toolsets</c>. Used internally by the Server area implementation and unwrapped to the toolset
///     list.
/// </summary>
internal sealed record HermesAgentToolsetsResult
{
    /// <summary>The object type discriminator (always <c>list</c>).</summary>
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    /// <summary>The reporting platform (the server sends <c>api_server</c>).</summary>
    [JsonPropertyName("platform")]
    public string? Platform { get; init; }

    /// <summary>The toolset entries.</summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<HermesAgentToolset> Data { get; init; } = [];
}
