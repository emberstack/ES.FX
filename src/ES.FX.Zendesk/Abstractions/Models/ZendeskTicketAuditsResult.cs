using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>A page of ticket audits (<c>GET /api/v2/tickets/{id}/audits.json</c>).</summary>
public sealed record ZendeskTicketAuditsResult
{
    [JsonPropertyName("audits")] public IReadOnlyList<ZendeskTicketAudit> Audits { get; init; } = [];
    [JsonPropertyName("count")] public int? Count { get; init; }
    [JsonPropertyName("next_page")] public string? NextPage { get; init; }
    [JsonPropertyName("previous_page")] public string? PreviousPage { get; init; }

    /// <summary>Sideloaded users (populated only when the request asks to include <c>users</c>).</summary>
    [JsonPropertyName("users")]
    public IReadOnlyList<ZendeskUser>? Users { get; init; }

    /// <summary>Sideloaded groups (populated only when the request asks to include <c>groups</c>).</summary>
    [JsonPropertyName("groups")]
    public IReadOnlyList<ZendeskGroup>? Groups { get; init; }

    /// <summary>Sideloaded organizations (populated only when the request asks to include <c>organizations</c>).</summary>
    [JsonPropertyName("organizations")]
    public IReadOnlyList<ZendeskOrganization>? Organizations { get; init; }
}