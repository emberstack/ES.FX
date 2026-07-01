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
}