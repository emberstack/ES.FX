using JetBrains.Annotations;

namespace ES.FX.OpenData.Countries.ISO3166;

/// <summary>
///     A country or area as defined by <b>ISO 3166-1</b>: alpha-2, alpha-3 and numeric codes, the English
///     short name, and (when the standard provides them) the official and common names plus the flag emoji.
///     This is the standard-faithful ISO 3166-1 record — for a curated, localization-friendly country list use
///     <c>ES.FX.OpenData.Countries</c> instead. Identity is by code —
///     compare on <see cref="Alpha2" /> / <see cref="NumericCode" />, not by reference.
/// </summary>
[PublicAPI]
public sealed class Iso3166Country
{
    /// <summary>ISO 3166-1 alpha-2 code (e.g. <c>"RO"</c>).</summary>
    public required string Alpha2 { get; init; }

    /// <summary>ISO 3166-1 alpha-3 code (e.g. <c>"ROU"</c>).</summary>
    public required string Alpha3 { get; init; }

    /// <summary>ISO 3166-1 numeric code (e.g. <c>642</c>).</summary>
    public required int NumericCode { get; init; }

    /// <summary>The English short name (e.g. <c>"Romania"</c>). The least-surprise default for display.</summary>
    public required string Name { get; init; }

    /// <summary>
    ///     The official (long-form) name when ISO 3166-1 defines one distinct from <see cref="Name" />
    ///     (e.g. <c>"Islamic Republic of Afghanistan"</c>); otherwise <c>null</c>.
    /// </summary>
    public string? OfficialName { get; init; }

    /// <summary>
    ///     The common name when ISO 3166-1 records one distinct from <see cref="Name" />
    ///     (e.g. <c>"South Korea"</c> for <c>"Korea, Republic of"</c>); otherwise <c>null</c>.
    /// </summary>
    public string? CommonName { get; init; }

    /// <summary>The flag as Unicode regional-indicator symbols (e.g. <c>"🇷🇴"</c>), when available.</summary>
    public string? Flag { get; init; }
}