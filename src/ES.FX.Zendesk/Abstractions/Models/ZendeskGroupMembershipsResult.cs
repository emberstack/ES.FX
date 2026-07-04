using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>A page of group memberships (<c>GET /api/v2/groups/{id}/memberships</c>).</summary>
public sealed record ZendeskGroupMembershipsResult
{
    [JsonPropertyName("group_memberships")]
    public IReadOnlyList<ZendeskGroupMembership> GroupMemberships { get; init; } = [];

    [JsonPropertyName("count")] public int? Count { get; init; }
    [JsonPropertyName("next_page")] public string? NextPage { get; init; }
    [JsonPropertyName("previous_page")] public string? PreviousPage { get; init; }

    /// <summary>Sideloaded users (populated only when the request asks to include <c>users</c>).</summary>
    [JsonPropertyName("users")]
    public IReadOnlyList<ZendeskUser>? Users { get; init; }

    /// <summary>Sideloaded groups (populated only when the request asks to include <c>groups</c>).</summary>
    [JsonPropertyName("groups")]
    public IReadOnlyList<ZendeskGroup>? Groups { get; init; }
}