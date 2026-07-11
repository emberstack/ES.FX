using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace ES.FX.OpenData.Romania.TerritorialUnits;

/// <summary>
///     Read access to the Romanian SIRUTA administrative-territorial register — all levels (counties, UATs,
///     localities). <see cref="Find" /> resolves a unit at any level and never throws, fixing the classic
///     "look up a county by code and blow up" hazard.
/// </summary>
[PublicAPI]
public interface IRomanianTerritorialUnitsDataset
{
    /// <summary>Every SIRUTA unit at every level.</summary>
    IReadOnlyList<TerritorialUnit> AllUnits { get; }

    /// <summary>All localities (SIRUTA level 3).</summary>
    IReadOnlyList<TerritorialUnit> Localities { get; }

    /// <summary>All UAT-level units (SIRUTA level 2: municipalities, towns, communes).</summary>
    IReadOnlyList<TerritorialUnit> Uats { get; }

    /// <summary>
    ///     The 42 first-level ISO 3166-2:RO subdivisions — 41 counties (județe) plus the Municipality of
    ///     Bucharest — enriched with ISO 3166-2 identity and the county seat.
    /// </summary>
    IReadOnlyList<RomanianCounty> Counties { get; }

    /// <summary>Gets a unit (any level) by SIRUTA code.</summary>
    /// <exception cref="KeyNotFoundException">Thrown when the code is unknown.</exception>
    TerritorialUnit this[int sirutaCode] { get; }

    /// <summary>Finds a unit (any level) by SIRUTA code, or <c>null</c> if unknown.</summary>
    TerritorialUnit? Find(int sirutaCode);

    /// <summary>Tries to get a unit (any level) by SIRUTA code.</summary>
    bool TryGet(int sirutaCode, [NotNullWhen(true)] out TerritorialUnit? unit);

    /// <summary>Finds a county by ISO code (<c>"RO-CJ"</c>) or abbreviation (<c>"CJ"</c>), or <c>null</c>.</summary>
    RomanianCounty? FindCounty(string isoOrAbbreviation);

    /// <summary>Finds a county by its own SIRUTA code, or <c>null</c> if the code is not a county.</summary>
    RomanianCounty? FindCounty(int sirutaCode);

    /// <summary>Returns the localities of a county by ISO code or abbreviation; empty if the county is unknown.</summary>
    IReadOnlyList<TerritorialUnit> GetLocalitiesInCounty(string isoOrAbbreviation);

    /// <summary>
    ///     Returns the UAT-level units (municipalities, towns, communes) of a county by ISO code or abbreviation;
    ///     empty if the county is unknown.
    /// </summary>
    IReadOnlyList<TerritorialUnit> GetUatsInCounty(string isoOrAbbreviation);

    /// <summary>
    ///     Returns the direct children of a unit — the units whose <see cref="TerritorialUnit.ParentSirutaCode" />
    ///     equals <paramref name="sirutaCode" /> (a county's UATs, a UAT's localities). Empty for a leaf or unknown
    ///     code.
    /// </summary>
    IReadOnlyList<TerritorialUnit> GetChildren(int sirutaCode);

    /// <summary>
    ///     Gets the parent (superior) unit of <paramref name="unit" />, or <c>null</c> at the top of the hierarchy
    ///     (a county's parent is the out-of-dataset national root).
    /// </summary>
    TerritorialUnit? GetParent(TerritorialUnit unit);

    /// <summary>Gets the enriched <see cref="RomanianCounty" /> a unit belongs to, or <c>null</c> if unresolvable.</summary>
    RomanianCounty? GetCounty(TerritorialUnit unit);

    /// <summary>
    ///     Searches localities (only) by name prefix, diacritic- and case-insensitively (the query is folded the
    ///     same way stored names are). Matching is a prefix over the base locality name — the disambiguating parent
    ///     suffix in <see cref="TerritorialUnit.DisplayName" /> is not searchable. Results are materialized and
    ///     ordered deterministically (sort factor, then name, then code).
    /// </summary>
    IReadOnlyList<TerritorialUnit> Search(string prefix);
}