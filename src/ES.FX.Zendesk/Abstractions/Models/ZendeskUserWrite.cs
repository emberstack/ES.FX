using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     The writable fields of a user, used for create, create-or-update, and update operations. Unset
///     (<c>null</c>) properties are omitted, so an update sends only the fields you set.
/// </summary>
public sealed record ZendeskUserWrite
{
    /// <summary>The user id — set ONLY for batch <c>update_many</c> items; leave <c>null</c> everywhere else.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; init; }

    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>
    ///     On create, becomes the primary e-mail identity. QUIRK: on update, Zendesk ADDS it as a secondary
    ///     identity instead of changing the primary — use the identity operations to change the primary e-mail.
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    /// <summary>The role — see <see cref="ZendeskUserRoles" />.</summary>
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("phone")] public string? Phone { get; init; }
    [JsonPropertyName("external_id")] public string? ExternalId { get; init; }

    /// <summary>Setting this removes the user's other organization memberships.</summary>
    [JsonPropertyName("organization_id")]
    public long? OrganizationId { get; init; }

    [JsonPropertyName("verified")] public bool? Verified { get; init; }
    [JsonPropertyName("suspended")] public bool? Suspended { get; init; }
    [JsonPropertyName("notes")] public string? Notes { get; init; }
    [JsonPropertyName("details")] public string? Details { get; init; }
    [JsonPropertyName("tags")] public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Custom user field values keyed by field key.</summary>
    [JsonPropertyName("user_fields")]
    public IReadOnlyDictionary<string, object?>? UserFields { get; init; }

    /// <summary>Suppresses the verification e-mail on create / create-or-update.</summary>
    [JsonPropertyName("skip_verify_email")]
    public bool? SkipVerifyEmail { get; init; }
}

/// <summary>The writable fields of a user identity (create / update).</summary>
public sealed record ZendeskUserIdentityWrite
{
    /// <summary>The identity type — see <see cref="ZendeskIdentityTypes" />.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("value")] public string? Value { get; init; }
    [JsonPropertyName("verified")] public bool? Verified { get; init; }

    /// <summary>Only writable at creation time; use the make-primary operation afterwards.</summary>
    [JsonPropertyName("primary")]
    public bool? Primary { get; init; }

    [JsonPropertyName("skip_verify_email")]
    public bool? SkipVerifyEmail { get; init; }
}

/// <summary>The <c>{ "deleted_user": {...} }</c> envelope returned by the permanent user deletion.</summary>
public sealed record ZendeskDeletedUserResponse
{
    [JsonPropertyName("deleted_user")] public ZendeskUser? DeletedUser { get; init; }
}