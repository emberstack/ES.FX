using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     Ticket/subscription counts related to a user (<c>GET /api/v2/users/{id}/related.json</c>,
///     <c>user_related</c> envelope).
/// </summary>
public sealed record ZendeskUserRelated
{
    [JsonPropertyName("assigned_tickets")] public long? AssignedTickets { get; init; }

    [JsonPropertyName("requested_tickets")]
    public long? RequestedTickets { get; init; }

    [JsonPropertyName("ccd_tickets")] public long? CcdTickets { get; init; }

    [JsonPropertyName("organization_subscriptions")]
    public long? OrganizationSubscriptions { get; init; }
}

/// <summary>The <c>{ "user_related": {...} }</c> envelope.</summary>
public sealed record ZendeskUserRelatedResponse
{
    [JsonPropertyName("user_related")] public ZendeskUserRelated? UserRelated { get; init; }
}