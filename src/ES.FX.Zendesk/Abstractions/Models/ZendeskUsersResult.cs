using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A page of users returned by <c>GET /api/v2/users/search.json</c>.
/// </summary>
public sealed record ZendeskUsersResult
{
    /// <summary>The matching users.</summary>
    [JsonPropertyName("users")]
    public IReadOnlyList<ZendeskUser> Users { get; init; } = [];

    /// <summary>The total number of matches.</summary>
    [JsonPropertyName("count")]
    public int Count { get; init; }

    /// <summary>The URL of the next page of results, if any.</summary>
    [JsonPropertyName("next_page")]
    public string? NextPage { get; init; }

    /// <summary>The URL of the previous page of results, if any.</summary>
    [JsonPropertyName("previous_page")]
    public string? PreviousPage { get; init; }

    /// <summary>Cursor-pagination metadata (populated when the request used cursor pagination).</summary>
    [JsonPropertyName("meta")]
    public ZendeskCursorMeta? Meta { get; init; }

    /// <summary>Sideloaded organizations (populated only when the request asks to include <c>organizations</c>).</summary>
    [JsonPropertyName("organizations")]
    public IReadOnlyList<ZendeskOrganization>? Organizations { get; init; }

    /// <summary>Sideloaded groups (populated only when the request asks to include <c>groups</c>).</summary>
    [JsonPropertyName("groups")]
    public IReadOnlyList<ZendeskGroup>? Groups { get; init; }

    /// <summary>Sideloaded identities (populated only when the request asks to include <c>identities</c>).</summary>
    [JsonPropertyName("identities")]
    public IReadOnlyList<ZendeskUserIdentity>? Identities { get; init; }
}