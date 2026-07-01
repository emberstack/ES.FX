using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A Zendesk user (see <c>GET /api/v2/users/{id}.json</c> and <c>GET /api/v2/users/me.json</c>).
/// </summary>
public sealed record ZendeskUser
{
    /// <summary>The automatically assigned user identifier.</summary>
    [JsonPropertyName("id")]
    public long Id { get; init; }

    /// <summary>The user's display name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The user's primary email address.</summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    /// <summary>The user's role (<c>end-user</c>, <c>agent</c>, or <c>admin</c>).</summary>
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    /// <summary>Whether the user is active.</summary>
    [JsonPropertyName("active")]
    public bool Active { get; init; }

    /// <summary>Whether the user's identity has been verified.</summary>
    [JsonPropertyName("verified")]
    public bool Verified { get; init; }

    /// <summary>The identifier of the user's organization, if any.</summary>
    [JsonPropertyName("organization_id")]
    public long? OrganizationId { get; init; }

    /// <summary>The user's time zone.</summary>
    [JsonPropertyName("time_zone")]
    public string? TimeZone { get; init; }

    /// <summary>The user's locale (e.g. <c>en-US</c>).</summary>
    [JsonPropertyName("locale")]
    public string? Locale { get; init; }

    /// <summary>When the user was created.</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>When the user was last updated.</summary>
    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; init; }
}