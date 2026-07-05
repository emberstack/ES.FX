using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The model listing returned by <c>GET /v1/models</c> (the OpenAI-style <c>{"object":"list","data":[...]}</c>
///     envelope).
/// </summary>
public sealed record HermesAgentModelsResult
{
    /// <summary>The object type discriminator (always <c>list</c>).</summary>
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    /// <summary>The advertised models. The first entry is the advertised model name; the rest are route aliases.</summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<HermesAgentModel> Data { get; init; } = [];
}
