using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace ES.FX.OpenData.Countries;

/// <summary>
///     Read access to the ISO 3166-1 countries dataset. Lookups follow the family contract: the indexer throws
///     for an unknown canonical code, <see cref="Find" /> / <see cref="TryGet" /> tolerate misses, and
///     <see cref="Resolve" /> additionally accepts non-standard alias codes.
/// </summary>
[PublicAPI]
public interface ICountriesDataset : IOpenDataset
{
    /// <summary>All countries, in source order.</summary>
    IReadOnlyList<Country> All { get; }

    /// <summary>The alias-tolerant superset map: every canonical alpha-2 code plus non-standard alias codes.</summary>
    IReadOnlyDictionary<string, Country> LookupMap { get; }

    /// <summary>Gets a country by ISO 3166-1 alpha-2 code (case-insensitive).</summary>
    /// <exception cref="KeyNotFoundException">Thrown when the code is not a known canonical alpha-2 code.</exception>
    Country this[string alpha2Code] { get; }

    /// <summary>Finds a country by alpha-2 code, or <c>null</c> if unknown.</summary>
    Country? Find(string alpha2Code);

    /// <summary>Tries to get a country by alpha-2 code.</summary>
    bool TryGet(string alpha2Code, [NotNullWhen(true)] out Country? country);

    /// <summary>Finds a country by ISO 3166-1 numeric code, or <c>null</c> if unknown.</summary>
    Country? FindByNumericCode(int numericCode);

    /// <summary>
    ///     Resolves any known code — canonical alpha-2 or a non-standard alias (e.g. <c>CYP</c>, <c>US-HI</c>) —
    ///     to a country, or <c>null</c> if unknown. Alias entries carry the territory's display name over the
    ///     canonical country's codes.
    /// </summary>
    Country? Resolve(string anyKnownCode);
}
