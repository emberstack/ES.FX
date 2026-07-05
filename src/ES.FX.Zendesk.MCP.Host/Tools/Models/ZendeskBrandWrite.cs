using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools.Models;

/// <summary>The writable fields of a brand (create / update).</summary>
public sealed record ZendeskBrandWrite
{
    /// <summary>The brand name. Required on create.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    ///     The brand subdomain — the {subdomain}.zendesk.com host segment, not a full URL. Required on create.
    /// </summary>
    [Description(
        "The brand's subdomain — the host segment only (e.g. \"brand1\" for brand1.zendesk.com), NOT a full URL. " +
        "Required on create.")]
    [JsonPropertyName("subdomain")]
    public string? Subdomain { get; init; }

    [JsonPropertyName("active")] public bool? Active { get; init; }

    /// <summary>
    ///     Marks this brand as the account default. Only one brand can be default; setting this true moves the
    ///     default off the previous brand.
    /// </summary>
    [JsonPropertyName("default")]
    public bool? Default { get; init; }

    [JsonPropertyName("brand_url")] public string? BrandUrl { get; init; }

    /// <summary>The custom host (CNAME) mapped to this brand, if any. Only admins can view this property.</summary>
    [JsonPropertyName("host_mapping")]
    public string? HostMapping { get; init; }

    [JsonPropertyName("signature_template")]
    public string? SignatureTemplate { get; init; }

    /// <summary>
    ///     Optional user scope: <c>account</c> (users are shared across account-scoped brands) or <c>brand</c>
    ///     (users created on this brand are isolated to it).
    /// </summary>
    [Description(
        "Optional user scope: \"account\" (users are shared across account-scoped brands) or \"brand\" " +
        "(users created on this brand are isolated to it).")]
    [JsonPropertyName("user_separation")]
    public string? UserSeparation { get; init; }
}