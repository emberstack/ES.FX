using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>An organization membership — links a user to an organization.</summary>
public sealed record ZendeskOrganizationMembership
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("user_id")] public long? UserId { get; init; }
    [JsonPropertyName("organization_id")] public long? OrganizationId { get; init; }

    /// <summary>Whether this is the user's default organization.</summary>
    [JsonPropertyName("default")]
    public bool? Default { get; init; }

    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>A page of organization memberships (<c>{ "organization_memberships": [...] }</c> envelope).</summary>
public sealed record ZendeskOrganizationMembershipsResult
{
    [JsonPropertyName("organization_memberships")]
    public IReadOnlyList<ZendeskOrganizationMembership> OrganizationMemberships { get; init; } = [];

    [JsonPropertyName("count")] public int? Count { get; init; }
    [JsonPropertyName("next_page")] public string? NextPage { get; init; }
    [JsonPropertyName("previous_page")] public string? PreviousPage { get; init; }
    [JsonPropertyName("meta")] public ZendeskCursorMeta? Meta { get; init; }
}