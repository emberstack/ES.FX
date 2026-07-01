using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A page of tickets returned by the Zendesk search API (<c>GET /api/v2/search.json?query=type:ticket ...</c>).
/// </summary>
public sealed record ZendeskTicketSearchResults
{
    /// <summary>The matching tickets.</summary>
    [JsonPropertyName("results")]
    public IReadOnlyList<ZendeskTicket> Results { get; init; } = [];

    /// <summary>The total number of matches.</summary>
    [JsonPropertyName("count")]
    public int Count { get; init; }

    /// <summary>The URL of the next page of results, if any.</summary>
    [JsonPropertyName("next_page")]
    public string? NextPage { get; init; }

    /// <summary>The URL of the previous page of results, if any.</summary>
    [JsonPropertyName("previous_page")]
    public string? PreviousPage { get; init; }

    /// <summary>Sideloaded users (populated only when the request asks to include <c>users</c>).</summary>
    [JsonPropertyName("users")]
    public IReadOnlyList<ZendeskUser>? Users { get; init; }

    /// <summary>Sideloaded groups (populated only when the request asks to include <c>groups</c>).</summary>
    [JsonPropertyName("groups")]
    public IReadOnlyList<ZendeskGroup>? Groups { get; init; }

    /// <summary>Sideloaded organizations (populated only when the request asks to include <c>organizations</c>).</summary>
    [JsonPropertyName("organizations")]
    public IReadOnlyList<ZendeskOrganization>? Organizations { get; init; }
}