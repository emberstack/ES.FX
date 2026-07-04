using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>A user identity — an email, phone number or social handle attached to a user.</summary>
public sealed record ZendeskUserIdentity
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("user_id")] public long? UserId { get; init; }

    /// <summary>The identity type: <c>email</c>, <c>phone_number</c>, <c>twitter</c>, <c>facebook</c>, ...</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The identity value (e-mail address, phone number, handle).</summary>
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("verified")] public bool? Verified { get; init; }
    [JsonPropertyName("primary")] public bool? Primary { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>The <c>{ "identity": {...} }</c> envelope.</summary>
public sealed record ZendeskUserIdentityResponse
{
    [JsonPropertyName("identity")] public ZendeskUserIdentity? Identity { get; init; }
}

/// <summary>A page of user identities (<c>{ "identities": [...] }</c> envelope).</summary>
public sealed record ZendeskUserIdentitiesResult
{
    [JsonPropertyName("identities")] public IReadOnlyList<ZendeskUserIdentity> Identities { get; init; } = [];
    [JsonPropertyName("count")] public int? Count { get; init; }
    [JsonPropertyName("next_page")] public string? NextPage { get; init; }
    [JsonPropertyName("previous_page")] public string? PreviousPage { get; init; }
    [JsonPropertyName("meta")] public ZendeskCursorMeta? Meta { get; init; }
}