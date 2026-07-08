using JetBrains.Annotations;

namespace ES.FX.OpenData.Romania.AdministrativeUnits;

/// <summary>
///     A single SIRUTA administrative-territorial unit at any level (county, UAT, or locality). Identity is by
///     <see cref="SirutaCode" /> — compare on the code, not by reference or structural equality.
/// </summary>
[PublicAPI]
public sealed class AdministrativeUnit
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

    /// <summary>The searchable form: diacritics removed, hyphens spaced, lower-cased. See <see cref="OpenDataText.Fold" />.</summary>
    public required string NormalizedName { get; init; }

    /// <summary>The SIRUTA hierarchy level (<c>NIV</c>): 1 = county, 2 = UAT, 3 = locality.</summary>
    public required int Level { get; init; }

    /// <summary>The SIRUTA unit type (<c>TIP</c>).</summary>
    public required SirutaUnitType Type { get; init; }

    /// <summary>
    ///     Urban/rural classification (<c>MED</c>); <see cref="AreaType.None" /> only for counties (UAT-level
    ///     units and localities carry a real urban/rural value).
    /// </summary>
    public required AreaType AreaType { get; init; }

    /// <summary>The postal code (<c>CODP</c>), or an empty string when the unit has none.</summary>
    public required string PostalCode { get; init; }

    /// <summary>The SIRUTA sorting factor (<c>FSL</c>).</summary>
    public required string SortingFactor { get; init; }

    /// <summary>The county abbreviation this unit belongs to (e.g. <c>"CJ"</c>), or empty if not resolvable.</summary>
    public required string CountyAbbreviation { get; init; }
}
