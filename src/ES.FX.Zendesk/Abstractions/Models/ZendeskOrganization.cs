using System.Text.Json;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>A Zendesk organization (see <c>GET /api/v2/organizations/{id}.json</c>).</summary>
public sealed record ZendeskOrganization
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("domain_names")] public IReadOnlyList<string>? DomainNames { get; init; }

    /// <summary>Free-text details about the organization.</summary>
    [JsonPropertyName("details")]
    public string? Details { get; init; }

    /// <summary>Internal notes about the organization (agent-only).</summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    /// <summary>The default group tickets from this organization are routed to.</summary>
    [JsonPropertyName("group_id")]
    public long? GroupId { get; init; }

    [JsonPropertyName("shared_tickets")] public bool? SharedTickets { get; init; }
    [JsonPropertyName("shared_comments")] public bool? SharedComments { get; init; }
    [JsonPropertyName("tags")] public IReadOnlyList<string>? Tags { get; init; }
    [JsonPropertyName("external_id")] public string? ExternalId { get; init; }

    /// <summary>Custom organization field values, keyed by field key.</summary>
    [JsonPropertyName("organization_fields")]
    public IReadOnlyDictionary<string, JsonElement>? OrganizationFields { get; init; }

    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
}