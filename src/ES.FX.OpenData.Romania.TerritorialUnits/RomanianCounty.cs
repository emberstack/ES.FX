using System.Text.Json.Serialization;
using ES.FX.OpenData.Countries.ISO3166;
using JetBrains.Annotations;

namespace ES.FX.OpenData.Romania.TerritorialUnits;

/// <summary>
///     A Romanian county (județ), enriched beyond raw SIRUTA. Its ISO 3166-2 identity (
///     <see cref="IsoCountrySubdivision" />,
///     <see cref="IsoCode" />, <see cref="Name" />) is sourced from <c>ES.FX.OpenData.Countries.ISO3166</c> — not
///     duplicated
///     here — while the plate abbreviation and county-seat name are Romania-specific curated
///     data. Identity is by <see cref="SirutaCode" /> / <see cref="Abbreviation" />.
/// </summary>
[PublicAPI]
public sealed class RomanianCounty
{
    /// <summary>The county's own SIRUTA code (e.g. <c>127</c> for Cluj).</summary>
    public required int SirutaCode { get; init; }

    /// <summary>
    ///     The county abbreviation used on vehicle registration plates (the Romanian "cod auto") and as the
    ///     ISO 3166-2 code suffix (e.g. <c>"CJ"</c>).
    /// </summary>
    public required string Abbreviation { get; init; }

    /// <summary>
    ///     The county's ISO 3166-2 subdivision (e.g. <c>RO-CJ</c>), sourced from <c>ES.FX.OpenData.Countries.ISO3166</c>.
    ///     Carries the official ISO code, name, and subdivision type; <see cref="IsoCode" /> and
    ///     <see cref="Name" /> are projected from it.
    /// </summary>
    public required Iso3166CountrySubdivision IsoCountrySubdivision { get; init; }

    /// <summary>The full ISO 3166-2 code (e.g. <c>"RO-CJ"</c>), from <see cref="IsoCountrySubdivision" />.</summary>
    public required string IsoCode { get; init; }

    /// <summary>
    ///     The county name (e.g. <c>"Cluj"</c>) — the clean ISO 3166-2 name from <see cref="IsoCountrySubdivision" />.
    ///     Note the same county fetched as an <see cref="TerritorialUnit" /> (via <c>Find(SirutaCode)</c>) carries
    ///     the raw SIRUTA form instead (<c>"Județul Cluj"</c>).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     The county residence (reședință de județ) — the SIRUTA unit flagged as the county seat
    ///     (<see cref="SirutaUnitType.MunicipalityCountyResidence" /> / <see cref="SirutaUnitType.TownCountyResidence" />,
    ///     e.g. <c>"Municipiul Cluj-Napoca"</c>). A live link into the dataset, not a copied name. <c>null</c> for
    ///     counties with no distinct seat unit in SIRUTA (Ilfov, Bucharest).
    /// </summary>
    public required TerritorialUnit? Residence { get; init; }

    /// <summary>
    ///     The localities (SIRUTA level 3) within this county. In-memory navigation into the dataset graph —
    ///     excluded from JSON serialization (it would otherwise inline every locality in the county).
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<TerritorialUnit> Localities { get; init; } = [];

    /// <summary>
    ///     The UAT-level units (SIRUTA level 2: municipalities, towns, communes) within this county. In-memory
    ///     navigation into the dataset graph — excluded from JSON serialization (it would otherwise inline every UAT).
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<TerritorialUnit> Uats { get; init; } = [];
}