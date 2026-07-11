using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace ES.FX.OpenData.Countries;

/// <summary>
///     Read access to the curated ISO 3166-2 country subdivisions (regions/states) — the subdivision counterpart
///     to <see cref="ICountriesDataset" />, and a separately registered dataset. Each <see cref="CountrySubdivision" />
///     carries a localized-name map, but only English (<c>en</c>) is curated today; other cultures fall back to the
///     English/ISO name via <see cref="CountrySubdivision.GetLocalizedName(System.Globalization.CultureInfo)" />.
///     Lookups follow the family contract: the indexer throws for an unknown code, while <see cref="Find" /> /
///     <see cref="TryGet" /> tolerate misses.
/// </summary>
/// <remarks>
///     Derives its identity from the raw ISO 3166-2 dataset
///     (<see cref="ES.FX.OpenData.Countries.ISO3166.IIso3166CountrySubdivisions" />). If you only need the raw ISO
///     subdivisions, register that dataset instead — registering both materializes the ~5,000 subdivisions twice.
/// </remarks>
[PublicAPI]
public interface ICountrySubdivisionsDataset
{
    /// <summary>All subdivisions, in source order.</summary>
    IReadOnlyList<CountrySubdivision> All { get; }

    /// <summary>Gets a subdivision by its full ISO 3166-2 code, e.g. <c>"US-HI"</c> (case-insensitive).</summary>
    /// <exception cref="KeyNotFoundException">Thrown when the code is not a known subdivision code.</exception>
    CountrySubdivision this[string code] { get; }

    /// <summary>Finds a subdivision by its full code (case-insensitive), or <c>null</c> if unknown.</summary>
    CountrySubdivision? Find(string code);

    /// <summary>Tries to get a subdivision by its full code (case-insensitive).</summary>
    bool TryGet(string code, [NotNullWhen(true)] out CountrySubdivision? subdivision);

    /// <summary>
    ///     All subdivisions of the country with the given ISO 3166-1 alpha-2 code (case-insensitive), in source
    ///     order. Returns an empty list when the country has no recorded subdivisions or is unknown. Includes
    ///     nested subdivisions — filter on <see cref="CountrySubdivision.Parent" /> for a top-level-only list.
    /// </summary>
    IReadOnlyList<CountrySubdivision> ForCountry(string countryAlpha2);
}