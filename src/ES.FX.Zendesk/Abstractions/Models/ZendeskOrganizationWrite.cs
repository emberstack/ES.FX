using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     The writable fields of an organization, used for create, create-or-update, and update operations. Unset
///     (<c>null</c>) properties are omitted.
/// </summary>
public sealed record ZendeskOrganizationWrite
{
    /// <summary>
    ///     The organization id — set for batch <c>update_many</c> items and for create-or-update matching; leave
    ///     <c>null</c> elsewhere.
    /// </summary>
    [JsonPropertyName("id")]
    public long? Id { get; init; }

    /// <summary>The (unique) organization name. Required on create.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Also a create-or-update matching key (case-insensitive).</summary>
    [JsonPropertyName("external_id")]
    public string? ExternalId { get; init; }

    /// <summary>QUIRK: overwrite-not-merge — always send the complete list.</summary>
    [JsonPropertyName("domain_names")]
    public IReadOnlyList<string>? DomainNames { get; init; }

    [JsonPropertyName("details")] public string? Details { get; init; }
    [JsonPropertyName("notes")] public string? Notes { get; init; }
    [JsonPropertyName("tags")] public IReadOnlyList<string>? Tags { get; init; }
    [JsonPropertyName("shared_tickets")] public bool? SharedTickets { get; init; }
    [JsonPropertyName("shared_comments")] public bool? SharedComments { get; init; }

    /// <summary>Custom organization field values keyed by field key.</summary>
    [JsonPropertyName("organization_fields")]
    public IReadOnlyDictionary<string, object?>? OrganizationFields { get; init; }
}

/// <summary>
///     An organization merge job (<c>POST /api/v2/organizations/{loser}/merge</c>). Async, but NOT a
///     <c>job_status</c> — poll <c>GET /api/v2/organization_merges/{id}</c>; the id is an opaque string.
/// </summary>
public sealed record ZendeskOrganizationMerge
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("winner_id")] public long? WinnerId { get; init; }
    [JsonPropertyName("loser_id")] public long? LoserId { get; init; }

    /// <summary><c>new</c>, <c>in progress</c>, <c>error</c>, or <c>complete</c>.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("url")] public string? Url { get; init; }
}

/// <summary>The <c>{ "organization_merge": {...} }</c> envelope.</summary>
public sealed record ZendeskOrganizationMergeResponse
{
    [JsonPropertyName("organization_merge")]
    public ZendeskOrganizationMerge? OrganizationMerge { get; init; }
}

/// <summary>The <c>{ "organization_membership": {...} }</c> envelope.</summary>
public sealed record ZendeskOrganizationMembershipResponse
{
    [JsonPropertyName("organization_membership")]
    public ZendeskOrganizationMembership? OrganizationMembership { get; init; }
}