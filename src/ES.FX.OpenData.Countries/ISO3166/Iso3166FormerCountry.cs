using JetBrains.Annotations;

namespace ES.FX.OpenData.Countries.ISO3166;

/// <summary>
///     A formerly used country code as recorded by <b>ISO 3166-3</b> — codes retired from ISO 3166-1 when a
///     country was renamed, merged, split, or dissolved (e.g. <c>"ANHH"</c>, Netherlands Antilles). The
///     four-letter <see cref="Alpha4" /> is the stable identity; the old alpha-2/alpha-3/numeric codes are
///     retained for historical lookups but may since have been reassigned to other countries.
/// </summary>
[PublicAPI]
public sealed class Iso3166FormerCountry
{
    /// <summary>
    ///     The ISO 3166-3 four-letter code (e.g. <c>"ANHH"</c>): the former alpha-2 followed by a two-letter
    ///     mnemonic. This is the record's stable identity and never collides across entries.
    /// </summary>
    public required string Alpha4 { get; init; }

    /// <summary>The former ISO 3166-1 alpha-3 code (e.g. <c>"ANT"</c>).</summary>
    public required string Alpha3 { get; init; }

    /// <summary>
    ///     The former ISO 3166-1 alpha-2 code (e.g. <c>"AN"</c>), or <c>null</c> if the record predates alpha-2
    ///     assignment. Note this code may since have been reassigned to a different country.
    /// </summary>
    public string? Alpha2 { get; init; }

    /// <summary>The former ISO 3166-1 numeric code, or <c>null</c> when the standard records none.</summary>
    public int? NumericCode { get; init; }

    /// <summary>The name the territory was known by (e.g. <c>"Netherlands Antilles"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>An explanatory note from the standard (e.g. how the territory was split), when present.</summary>
    public string? Comment { get; init; }

    /// <summary>
    ///     The withdrawal date as recorded by ISO 3166-3 — a year (<c>"1977"</c>) or full date
    ///     (<c>"2010-12-15"</c>); kept as the source string since the granularity varies. <c>null</c> if unknown.
    /// </summary>
    public string? WithdrawalDate { get; init; }
}