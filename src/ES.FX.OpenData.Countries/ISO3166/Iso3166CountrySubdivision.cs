using JetBrains.Annotations;

namespace ES.FX.OpenData.Countries.ISO3166;

/// <summary>
///     A country subdivision (state, province, region, district, …) as defined by <b>ISO 3166-2</b>. The
///     <see cref="Code" /> is the full subdivision code (e.g. <c>"US-HI"</c> for Hawaii); its country prefix is
///     surfaced as <see cref="CountryAlpha2" />. Identity is by <see cref="Code" />.
/// </summary>
[PublicAPI]
public sealed class Iso3166CountrySubdivision
{
    /// <summary>The full ISO 3166-2 subdivision code (e.g. <c>"US-HI"</c>, <c>"RO-B"</c>).</summary>
    public required string Code { get; init; }

    /// <summary>The ISO 3166-1 alpha-2 code of the country this subdivision belongs to (e.g. <c>"US"</c>).</summary>
    public required string CountryAlpha2 { get; init; }

    /// <summary>The subdivision name in its ISO-recorded (local) form (e.g. <c>"Hawaii"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>The subdivision category (e.g. <c>"State"</c>, <c>"Municipality"</c>, <c>"Region"</c>).</summary>
    public required string Type { get; init; }

    /// <summary>
    ///     The parent subdivision's <see cref="Code" /> when this subdivision is nested (e.g. an Azerbaijani
    ///     rayon whose parent is an autonomous republic); otherwise <c>null</c>.
    /// </summary>
    public string? Parent { get; init; }
}