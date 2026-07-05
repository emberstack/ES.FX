using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools.Models;

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

    /// <summary>
    ///     The role — see <see cref="ZendeskUserRoles" />. Allowed roles are <c>end-user</c>, <c>agent</c>, or
    ///     <c>admin</c>. Omitting the role creates an end user.
    /// </summary>
    [Description("The user's role. Allowed values: \"end-user\", \"agent\", \"admin\".")]
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("phone")] public string? Phone { get; init; }

    /// <summary>
    ///     An arbitrary string linking the user to a record in an external system; used by create-or-update to match
    ///     an existing user (the match is case-insensitive, but the stored external id is updated to the case you
    ///     supply).
    /// </summary>
    [JsonPropertyName("external_id")]
    public string? ExternalId { get; init; }

    /// <summary>
    ///     Assigns the user to that organization; setting this removes the user's other organization memberships.
    /// </summary>
    [JsonPropertyName("organization_id")]
    public long? OrganizationId { get; init; }

    [JsonPropertyName("verified")] public bool? Verified { get; init; }
    [JsonPropertyName("suspended")] public bool? Suspended { get; init; }
    [JsonPropertyName("notes")] public string? Notes { get; init; }
    [JsonPropertyName("details")] public string? Details { get; init; }
    [JsonPropertyName("tags")] public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Which tickets this user may access.</summary>
    [Description(
        "Which tickets this user may access. Allowed values: \"organization\", \"groups\", \"assigned\", \"requested\", or null.")]
    [JsonPropertyName("ticket_restriction")]
    public string? TicketRestriction { get; init; }

    /// <summary>Custom user field values keyed by field key.</summary>
    [Description(
        "Custom user-field values as a JSON object keyed by each field's KEY (its 'key', not a numeric id): " +
        "{\"<field_key>\": <value>}. For dropdown/multiselect fields the value is the option's tag value (its " +
        "'value', not its title). Value type follows the field type (checkbox=boolean, date=\"YYYY-MM-DD\", " +
        "number). Field keys are configured per tenant in Admin Center; obtain them from your Zendesk admin " +
        "(they also appear under user_fields on an existing record via users_get).")]
    [JsonPropertyName("user_fields")]
    public IReadOnlyDictionary<string, object?>? UserFields { get; init; }

    /// <summary>Suppresses the verification e-mail on create / create-or-update.</summary>
    [JsonPropertyName("skip_verify_email")]
    public bool? SkipVerifyEmail { get; init; }
}