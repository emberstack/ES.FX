using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools.Models;

/// <summary>The writable fields of a user identity (create / update).</summary>
public sealed record ZendeskUserIdentityWrite
{
    /// <summary>
    ///     The identity type — see <see cref="ZendeskIdentityTypes" />. Allowed values are <c>email</c>,
    ///     <c>phone_number</c>, <c>twitter</c>, <c>facebook</c>, <c>google</c>, <c>agent_forwarding</c> (also
    ///     <c>any_channel</c>, <c>foreign</c>, <c>sdk</c>, <c>messaging</c>).
    /// </summary>
    [JsonPropertyName("type")]
    [Description(
        "The identity type. Allowed values: \"email\", \"phone_number\", \"twitter\", \"facebook\", \"google\", " +
        "\"agent_forwarding\", \"any_channel\", \"foreign\", \"sdk\", \"messaging\".")]
    public string? Type { get; init; }

    [JsonPropertyName("value")] public string? Value { get; init; }

    /// <summary>
    ///     On update, set to <c>true</c> to mark the identity verified or <c>false</c> to unverify it. The primary
    ///     attribute cannot be changed here — use the make-primary operation instead.
    /// </summary>
    [JsonPropertyName("verified")]
    public bool? Verified { get; init; }

    /// <summary>Only writable at creation time; use the make-primary operation afterwards.</summary>
    [JsonPropertyName("primary")]
    public bool? Primary { get; init; }

    /// <summary>
    ///     Set to <c>true</c> to add the identity without sending a verification e-mail. Does NOT apply when updating
    ///     your own agent profile — a welcome or verification e-mail is sent regardless.
    /// </summary>
    [JsonPropertyName("skip_verify_email")]
    public bool? SkipVerifyEmail { get; init; }
}