using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A Zendesk count record (<c>{ "count": { "value": ..., "refreshed_at": ... } }</c> envelope). Counts over
///     100,000 are approximate — cached and refreshed roughly every 24 hours (<see cref="RefreshedAt" /> may be
///     <c>null</c> while Zendesk recomputes).
/// </summary>
public sealed record ZendeskCount
{
    /// <summary>The (possibly cached) count value.</summary>
    [JsonPropertyName("value")]
    public long? Value { get; init; }

    /// <summary>When the cached value was last refreshed, if reported.</summary>
    [JsonPropertyName("refreshed_at")]
    public DateTimeOffset? RefreshedAt { get; init; }
}

/// <summary>The <c>{ "count": {...} }</c> envelope wrapping <see cref="ZendeskCount" />.</summary>
public sealed record ZendeskCountResponse
{
    [JsonPropertyName("count")] public ZendeskCount? Count { get; init; }
}