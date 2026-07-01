using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A Zendesk group — a collection of agents tickets can be assigned to (see <c>GET /api/v2/groups/{id}.json</c>).
///     Resolves the <c>group_id</c> carried on tickets and organizations to a human-readable name.
/// </summary>
public sealed record ZendeskGroup
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }

    /// <summary>Whether this is the account's default group.</summary>
    [JsonPropertyName("default")]
    public bool? Default { get; init; }

    /// <summary>Whether the group is public (visible to all agents) rather than private.</summary>
    [JsonPropertyName("is_public")]
    public bool? IsPublic { get; init; }

    /// <summary>Whether the group has been deleted.</summary>
    [JsonPropertyName("deleted")]
    public bool? Deleted { get; init; }

    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
}