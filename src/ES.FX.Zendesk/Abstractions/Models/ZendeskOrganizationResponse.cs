using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>Envelope for a single-organization response (<c>{ "organization": { ... } }</c>).</summary>
public sealed record ZendeskOrganizationResponse
{
    [JsonPropertyName("organization")] public ZendeskOrganization? Organization { get; init; }
}