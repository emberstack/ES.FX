using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace ES.FX.OpenData.Countries.ISO3166;

/// <summary>
///     Read access to the <b>ISO 3166-1</b> country-code dataset. Lookups follow the family contract: the
///     indexer throws for an unknown alpha-2 code, while <see cref="Find" /> / <see cref="TryGet" /> tolerate
///     misses.
/// </summary>
[PublicAPI]
public interface IIso3166Countries
{
    /// <summary>All countries, in source order.</summary>
    IReadOnlyList<Iso3166Country> All { get; }

    /// <summary>Gets a country by ISO 3166-1 alpha-2 code (case-insensitive).</summary>
    /// <exception cref="KeyNotFoundException">Thrown when the code is not a known alpha-2 code.</exception>
    Iso3166Country this[string alpha2] { get; }

    /// <summary>Finds a country by alpha-2 code (case-insensitive), or <c>null</c> if unknown.</summary>
    Iso3166Country? Find(string alpha2);

    /// <summary>Tries to get a country by alpha-2 code (case-insensitive).</summary>
    bool TryGet(string alpha2, [NotNullWhen(true)] out Iso3166Country? country);

    /// <summary>Finds a country by alpha-3 code (case-insensitive), or <c>null</c> if unknown.</summary>
    Iso3166Country? FindByAlpha3(string alpha3);

    /// <summary>Finds a country by ISO 3166-1 numeric code, or <c>null</c> if unknown.</summary>
    Iso3166Country? FindByNumericCode(int numericCode);
}