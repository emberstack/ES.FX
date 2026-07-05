using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools.Models;

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

    /// <summary>
    ///     The organization name. Required on create and must be unique across the account. Leading/trailing
    ///     whitespace is trimmed before validation, so names differing only by whitespace are treated as duplicates
    ///     (e.g. "API Company" and "API Company ").
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    ///     A unique external record key. Case-insensitive — "company1" and "Company1" are treated as the same value.
    ///     Also a create-or-update matching key.
    /// </summary>
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

    /// <summary>
    ///     Custom organization field values keyed by the field's key; each value is a string or a number.
    /// </summary>
    [JsonPropertyName("organization_fields")]
    [Description(
        "Custom organization-field values as a JSON object keyed by each field's KEY (its 'key', not a numeric " +
        "id): {\"<field_key>\": <value>}. For dropdown fields the value is the option's tag value. Value type " +
        "follows the field type (checkbox=boolean, date=\"YYYY-MM-DD\", number). Field keys are configured per " +
        "tenant in Admin Center; obtain them from your Zendesk admin (they also appear under organization_fields " +
        "on an existing record via organizations_get).")]
    public IReadOnlyDictionary<string, object?>? OrganizationFields { get; init; }
}