using JetBrains.Annotations;

namespace ES.FX.OpenData.Romania.AdministrativeUnits;

/// <summary>
///     A Romanian county (județ), enriched beyond raw SIRUTA with its ISO 3166-2 code, plate abbreviation,
///     county-seat name, and national ID document series. Identity is by <see cref="SirutaCode" /> /
///     <see cref="Abbreviation" />.
/// </summary>
[PublicAPI]
public sealed class RomanianCounty
{
    /// <summary>The county's own SIRUTA code (e.g. <c>54975</c> for Cluj).</summary>
    public required int SirutaCode { get; init; }

    /// <summary>The full ISO 3166-2 code (e.g. <c>"RO-CJ"</c>).</summary>
    public required string IsoCode { get; init; }

    /// <summary>
    ///     The county abbreviation used on vehicle plates and as the ISO 3166-2 code suffix (e.g. <c>"CJ"</c>).
    ///     This is distinct from <see cref="NationalIdSeries" />.
    /// </summary>
    public required string Abbreviation { get; init; }

    /// <summary>The county name (e.g. <c>"Cluj"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>The name of the county residence / seat (e.g. <c>"Cluj-Napoca"</c>).</summary>
    public required string ResidenceName { get; init; }

    /// <summary>
    ///     The national identity-document (CI) series letters issued for this county (e.g. <c>["KX", "CJ"]</c>).
    ///     Curated data; not part of SIRUTA. Distinct from <see cref="Abbreviation" />.
    /// </summary>
    public required IReadOnlyList<string> NationalIdSeries { get; init; }

    /// <summary>The localities (SIRUTA level 3) within this county.</summary>
    public required IReadOnlyList<AdministrativeUnit> Localities { get; init; }
}
