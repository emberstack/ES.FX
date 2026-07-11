using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace ES.FX.OpenData.Countries.ISO3166;

/// <summary>
///     Read access to the <b>ISO 3166-3</b> formerly-used-country-code dataset. The four-letter code is the
///     identity: the indexer and <see cref="Find" /> key on it, throwing / returning <c>null</c> respectively
///     for unknown codes. <see cref="FindByAlpha2" /> offers a historical alpha-2 lookup.
/// </summary>
[PublicAPI]
public interface IIso3166FormerCountries
{
    /// <summary>All formerly used country codes, in source order.</summary>
    IReadOnlyList<Iso3166FormerCountry> All { get; }

    /// <summary>Gets a former country by its ISO 3166-3 four-letter code, e.g. <c>"ANHH"</c> (case-insensitive).</summary>
    /// <exception cref="KeyNotFoundException">Thrown when the code is not a known four-letter code.</exception>
    Iso3166FormerCountry this[string alpha4] { get; }

    /// <summary>Finds a former country by its four-letter code (case-insensitive), or <c>null</c> if unknown.</summary>
    Iso3166FormerCountry? Find(string alpha4);

    /// <summary>Tries to get a former country by its four-letter code (case-insensitive).</summary>
    bool TryGet(string alpha4, [NotNullWhen(true)] out Iso3166FormerCountry? formerCountry);

    /// <summary>
    ///     Finds a former country by its retired alpha-2 code (case-insensitive), or <c>null</c> if none match.
    ///     Note a former alpha-2 code may since have been reassigned to a current country.
    /// </summary>
    Iso3166FormerCountry? FindByAlpha2(string alpha2);
}