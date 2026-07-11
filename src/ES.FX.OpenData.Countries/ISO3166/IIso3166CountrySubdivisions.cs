using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace ES.FX.OpenData.Countries.ISO3166;

/// <summary>
///     Read access to the <b>ISO 3166-2</b> subdivision-code dataset. Lookups follow the family contract: the
///     indexer throws for an unknown code, while <see cref="Find" /> / <see cref="TryGet" /> tolerate misses.
/// </summary>
[PublicAPI]
public interface IIso3166CountrySubdivisions
{
    /// <summary>All subdivisions, in source order.</summary>
    IReadOnlyList<Iso3166CountrySubdivision> All { get; }

    /// <summary>Gets a subdivision by its full ISO 3166-2 code, e.g. <c>"US-HI"</c> (case-insensitive).</summary>
    /// <exception cref="KeyNotFoundException">Thrown when the code is not a known subdivision code.</exception>
    Iso3166CountrySubdivision this[string code] { get; }

    /// <summary>Finds a subdivision by its full code (case-insensitive), or <c>null</c> if unknown.</summary>
    Iso3166CountrySubdivision? Find(string code);

    /// <summary>Tries to get a subdivision by its full code (case-insensitive).</summary>
    bool TryGet(string code, [NotNullWhen(true)] out Iso3166CountrySubdivision? subdivision);

    /// <summary>
    ///     All subdivisions of the country with the given ISO 3166-1 alpha-2 code (case-insensitive), in source
    ///     order. Returns an empty list when the country has no recorded subdivisions or is unknown.
    /// </summary>
    IReadOnlyList<Iso3166CountrySubdivision> ForCountry(string countryAlpha2);
}