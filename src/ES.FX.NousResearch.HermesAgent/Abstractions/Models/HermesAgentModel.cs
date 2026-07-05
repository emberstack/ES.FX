using System.Text.Json;
using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     A model entry advertised by the server (see <c>GET /v1/models</c>). The first entry of the listing is the
///     advertised model; additional entries are configured model-route aliases.
/// </summary>
public sealed record HermesAgentModel
{
    /// <summary>The model identifier (the advertised model name, or a model-route alias).</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The object type discriminator (always <c>model</c>).</summary>
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    /// <summary>
    ///     The creation time in unix seconds. The server stamps the current time at request time — this is not a
    ///     real model-creation timestamp.
    /// </summary>
    [JsonPropertyName("created")]
    public long? Created { get; init; }

    /// <summary>The owner of the model (the server reports <c>hermes</c>).</summary>
    [JsonPropertyName("owned_by")]
    public string? OwnedBy { get; init; }

    /// <summary>The legacy OpenAI permission list (the server always sends an empty array).</summary>
    [JsonPropertyName("permission")]
    public IReadOnlyList<JsonElement> Permission { get; init; } = [];

    /// <summary>
    ///     The root model name. For the advertised model this equals <see cref="Id" />; for a model-route alias it
    ///     is the route's resolved backend model name.
    /// </summary>
    [JsonPropertyName("root")]
    public string? Root { get; init; }

    /// <summary>
    ///     The parent model. <c>null</c> for the advertised model; the advertised model name for a model-route
    ///     alias.
    /// </summary>
    [JsonPropertyName("parent")]
    public string? Parent { get; init; }
}
