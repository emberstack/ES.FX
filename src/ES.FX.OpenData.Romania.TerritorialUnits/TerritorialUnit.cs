using JetBrains.Annotations;

namespace ES.FX.OpenData.Romania.TerritorialUnits;

/// <summary>
///     A single SIRUTA administrative-territorial unit at any level (county, UAT, or locality). Identity is by
///     <see cref="SirutaCode" /> — compare on the code, not by reference or structural equality.
/// </summary>
[PublicAPI]
public sealed class TerritorialUnit
{
    /// <summary>The SIRUTA code of this unit.</summary>
    public required int SirutaCode { get; init; }

    /// <summary>The SIRUTA code of the parent (superior) unit (<c>SIRSUP</c>).</summary>
    public required int ParentSirutaCode { get; init; }

    /// <summary>The canonical name, title-cased with diacritics preserved (e.g. <c>"Bărăbanț"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>
    ///     The UI-friendly name. Equals <see cref="Name" /> except for villages belonging to a commune
    ///     (<see cref="SirutaUnitType.VillageBelongingToCommune" />), which are disambiguated as
    ///     <c>"Village (Parent)"</c>.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    ///     The search form: diacritics removed, hyphens spaced, trimmed, and lower-cased with the invariant
    ///     culture (e.g. <c>"braila"</c>). Search queries are folded the same way — match on this, not
    ///     <see cref="Name" />.
    /// </summary>
    public required string SearchNormalizedName { get; init; }

    /// <summary>
    ///     A diacritic-free display form: diacritics removed, trimmed, and title-cased (e.g. <c>"Brăila"</c> →
    ///     <c>"Braila"</c>). Readable ASCII for display or interop; keeps hyphens and casing (unlike
    ///     <see cref="SearchNormalizedName" />).
    /// </summary>
    public required string DisplayNormalizedName { get; init; }

    /// <summary>The SIRUTA hierarchy level (<c>NIV</c>): 1 = county, 2 = UAT, 3 = locality.</summary>
    public required int Level { get; init; }

    /// <summary>The SIRUTA unit type (<c>TIP</c>).</summary>
    public required SirutaUnitType Type { get; init; }

    /// <summary>
    ///     Urban/rural classification (<c>MED</c>); <see cref="AreaType.None" /> only for counties (UAT-level
    ///     units and localities carry a real urban/rural value).
    /// </summary>
    public required AreaType AreaType { get; init; }

    /// <summary>The postal code (<c>CODP</c>), or <c>null</c> when the unit has none (counties and UAT-level rows).</summary>
    public required string? PostalCode { get; init; }

    /// <summary>The SIRUTA sorting factor (<c>FSL</c>).</summary>
    public required string SortingFactor { get; init; }

    /// <summary>The county abbreviation this unit belongs to (e.g. <c>"CJ"</c>), or empty if not resolvable.</summary>
    public required string CountyAbbreviation { get; init; }
}