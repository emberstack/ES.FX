using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace ES.FX.OpenData.Romania.AdministrativeUnits;

/// <summary>
///     Read access to the Romanian SIRUTA administrative-territorial register — all levels (counties, UATs,
///     localities). <see cref="Find" /> resolves a unit at any level and never throws, fixing the classic
///     "look up a county by code and blow up" hazard.
/// </summary>
[PublicAPI]
public interface IRomanianAdministrativeUnitsDataset : IOpenDataset
{
    /// <summary>Every SIRUTA unit at every level.</summary>
    IReadOnlyList<AdministrativeUnit> AllUnits { get; }

    /// <summary>All localities (SIRUTA level 3).</summary>
    IReadOnlyList<AdministrativeUnit> Localities { get; }

    /// <summary>All UAT-level units (SIRUTA level 2: municipalities, towns, communes).</summary>
    IReadOnlyList<AdministrativeUnit> Uats { get; }

    /// <summary>The 42 counties (județe), enriched with ISO codes and national ID series.</summary>
    IReadOnlyList<RomanianCounty> Counties { get; }

    /// <summary>Gets a unit (any level) by SIRUTA code.</summary>
    /// <exception cref="KeyNotFoundException">Thrown when the code is unknown.</exception>
    AdministrativeUnit this[int sirutaCode] { get; }

    /// <summary>Finds a unit (any level) by SIRUTA code, or <c>null</c> if unknown.</summary>
    AdministrativeUnit? Find(int sirutaCode);

    /// <summary>Tries to get a unit (any level) by SIRUTA code.</summary>
    bool TryGet(int sirutaCode, [NotNullWhen(true)] out AdministrativeUnit? unit);

    /// <summary>Finds a county by ISO code (<c>"RO-CJ"</c>) or abbreviation (<c>"CJ"</c>), or <c>null</c>.</summary>
    RomanianCounty? FindCounty(string isoOrAbbreviation);

    /// <summary>Returns the localities of a county by ISO code or abbreviation; empty if the county is unknown.</summary>
    IReadOnlyList<AdministrativeUnit> GetLocalitiesInCounty(string isoOrAbbreviation);

    /// <summary>
    ///     Searches localities by name prefix, diacritic- and case-insensitively (the query is folded the same
    ///     way stored names are). Results are ordered deterministically (sort factor, then name, then code).
    /// </summary>
    IEnumerable<AdministrativeUnit> Search(string prefix);
}
