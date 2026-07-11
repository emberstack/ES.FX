using ES.FX.OpenData.Romania.TerritorialUnits;
using JetBrains.Annotations;

namespace ES.FX.OpenData.Romania.Anaf.VatCheck;

/// <summary>
///     A structured ANAF address (registered office or fiscal domicile). Raw ANAF codes are preserved:
///     <see cref="LocalityCode" /> is an ANAF-internal code (NOT a SIRUTA code), <see cref="CountyCode" /> is the
///     SIRUTA county (JUD) number as a string, and <see cref="CountyAutoCode" /> is the plate abbreviation (e.g.
///     <c>"GJ"</c>). Everything that points at a Romanian territorial unit is resolved against
///     <c>ES.FX.OpenData.Romania.TerritorialUnits</c>: <see cref="RomanianLocality" /> (the exact
///     locality), <see cref="RomanianUat" /> (its municipality/town/commune), and
///     <see cref="AnafVatCheckAddress.RomanianCounty" /> (its county).
/// </summary>
[PublicAPI]
public sealed record AnafVatCheckAddress
{
    /// <summary>The street name (<c>denumire_Strada</c>).</summary>
    public string? Street { get; init; }

    /// <summary>The street number (<c>numar_Strada</c>).</summary>
    public string? StreetNumber { get; init; }

    /// <summary>The locality name (<c>denumire_Localitate</c>, e.g. <c>"Mun. Târgu Jiu"</c>).</summary>
    public string? Locality { get; init; }

    /// <summary>The ANAF locality code (<c>cod_Localitate</c>). Raw ANAF code — NOT a SIRUTA code.</summary>
    public string? LocalityCode { get; init; }

    /// <summary>The county name (<c>denumire_Judet</c>).</summary>
    public string? County { get; init; }

    /// <summary>The county code (<c>cod_Judet</c>) — the SIRUTA JUD number as a string (e.g. <c>"18"</c>).</summary>
    public string? CountyCode { get; init; }

    /// <summary>The county auto/plate abbreviation (<c>cod_JudetAuto</c>, e.g. <c>"GJ"</c>).</summary>
    public string? CountyAutoCode { get; init; }

    /// <summary>The postal code (<c>cod_Postal</c>).</summary>
    public string? PostalCode { get; init; }

    /// <summary>The country (<c>tara</c>), when present.</summary>
    public string? Country { get; init; }

    /// <summary>Free-form address details (<c>detalii_Adresa</c>).</summary>
    public string? Details { get; init; }

    /// <summary>
    ///     The exact SIRUTA territorial unit (usually a locality) this address resolves to — matched from
    ///     <see cref="CountyCode" /> + <see cref="LocalityCode" /> through the embedded ANAF→SIRUTA crosswalk and
    ///     looked up in <c>ES.FX.OpenData.Romania.TerritorialUnits</c>. <c>null</c> when the pair is unknown or the
    ///     resolved SIRUTA code is not present in the shipped dataset edition.
    /// </summary>
    public TerritorialUnit? RomanianLocality { get; init; }

    /// <summary>
    ///     The UAT-level unit (municipality, town, or commune — the one with the primărie) that
    ///     <see cref="RomanianLocality" /> belongs to: the unit itself when it is already a UAT, otherwise
    ///     its parent. <c>null</c> when the locality could not be resolved.
    /// </summary>
    public TerritorialUnit? RomanianUat { get; init; }

    /// <summary>
    ///     The SIRUTA county this address resolves to — from <see cref="CountyAutoCode" />, falling back to the
    ///     county of <see cref="RomanianLocality" />. <c>null</c> when neither the code nor the locality is
    ///     recognized.
    /// </summary>
    public RomanianCounty? RomanianCounty { get; init; }
}