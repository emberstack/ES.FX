using JetBrains.Annotations;

namespace ES.FX.OpenData.Romania.AdministrativeUnits;

/// <summary>
///     The SIRUTA unit type (<c>TIP</c>) of an administrative unit. Values are the raw SIRUTA codes; an unknown
///     future code casts to <see cref="Unknown" />-adjacent numeric values rather than throwing.
/// </summary>
[PublicAPI]
public enum SirutaUnitType
{
    /// <summary>Unknown / unmapped SIRUTA type.</summary>
    Unknown = 0,

    /// <summary>County (județ) or the Municipality of Bucharest — <c>TIP 40</c>.</summary>
    County = 40,

    /// <summary>Municipality that is a county residence (municipiu reședință de județ) — <c>TIP 1</c>.</summary>
    MunicipalityCountyResidence = 1,

    /// <summary>Municipality other than a county residence — <c>TIP 4</c>.</summary>
    Municipality = 4,

    /// <summary>Town subordinate to a county (oraș aparținător de județ) — <c>TIP 2</c>.</summary>
    TownSubordinateToCounty = 2,

    /// <summary>Town that is a county residence (oraș reședință de județ) — <c>TIP 5</c>.</summary>
    TownCountyResidence = 5,

    /// <summary>Commune (comună) — <c>TIP 3</c>.</summary>
    Commune = 3,

    /// <summary>Component locality that is a municipality residence — <c>TIP 9</c>.</summary>
    ComponentLocalityMunicipalityResidence = 9,

    /// <summary>Village belonging to a municipality (sat aparținător de municipiu) — <c>TIP 11</c>.</summary>
    VillageBelongingToMunicipality = 11,

    /// <summary>Component locality of a municipality other than the residence — <c>TIP 10</c>.</summary>
    ComponentLocalityMunicipality = 10,

    /// <summary>Component locality that is a town residence — <c>TIP 17</c>.</summary>
    ComponentLocalityTownResidence = 17,

    /// <summary>Village subordinate to a town (sat subordonat oraș) — <c>TIP 19</c>.</summary>
    VillageSubordinateToTown = 19,

    /// <summary>Component locality of a town other than the residence — <c>TIP 18</c>.</summary>
    ComponentLocalityTown = 18,

    /// <summary>Village that is a commune residence (sat reședință de comună) — <c>TIP 22</c>.</summary>
    VillageCommuneResidence = 22,

    /// <summary>
    ///     Village belonging to a commune, other than the residence (sat aparținător de comună) — <c>TIP 23</c>.
    ///     These get a disambiguating display name of the form <c>"Village (Commune)"</c>.
    /// </summary>
    VillageBelongingToCommune = 23,

    /// <summary>A sector of the Municipality of Bucharest — <c>TIP 6</c>.</summary>
    BucharestSector = 6
}
