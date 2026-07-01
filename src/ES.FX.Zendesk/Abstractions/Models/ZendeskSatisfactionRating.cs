using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     The customer satisfaction (CSAT) rating on a ticket. <see cref="Score" /> is typically
///     <c>offered</c>, <c>unoffered</c>, <c>good</c>, or <c>bad</c>; <see cref="Comment" /> is the optional
///     free-text the requester left with the rating.
/// </summary>
public sealed record ZendeskSatisfactionRating
{
    [JsonPropertyName("id")] public long? Id { get; init; }
    [JsonPropertyName("score")] public string? Score { get; init; }
    [JsonPropertyName("comment")] public string? Comment { get; init; }
}