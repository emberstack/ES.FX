using JetBrains.Annotations;

namespace ES.FX.OpenData.Romania.Anaf.VatCheck;

/// <summary>A company record from the ANAF VAT-payer registry (PlatitorTvaRest v9).</summary>
[PublicAPI]
public sealed record AnafCompanyVatCheckResult
{
    /// <summary>The unique identification code (CUI/CIF) — the company's fiscal identifier.</summary>
    public required long UniqueIdentificationCode { get; init; }

    /// <summary>The registered company name (<c>denumire</c>).</summary>
    public required string Name { get; init; }

    /// <summary>The full fiscal-domicile address as a single display string (<c>adresa</c>).</summary>
    public string? Address { get; init; }

    /// <summary>The registered phone number (<c>telefon</c>).</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>The registered fax number (<c>fax</c>).</summary>
    public string? Fax { get; init; }

    /// <summary>The postal code of the fiscal domicile (<c>codPostal</c>).</summary>
    public string? PostalCode { get; init; }

    /// <summary>The trade-register number (<c>nrRegCom</c>).</summary>
    public string? RegistrationNumber { get; init; }

    /// <summary>The primary CAEN activity code (<c>cod_CAEN</c>), kept as the raw code.</summary>
    public string? CaenCode { get; init; }

    /// <summary>The company IBAN (<c>iban</c>), when published.</summary>
    public string? Iban { get; init; }

    /// <summary>The legal form (<c>forma_juridica</c>).</summary>
    public string? LegalForm { get; init; }

    /// <summary>The organization form (<c>forma_organizare</c>).</summary>
    public string? OrganizationForm { get; init; }

    /// <summary>The ownership form (<c>forma_de_proprietate</c>).</summary>
    public string? OwnershipForm { get; init; }

    /// <summary>The competent fiscal authority (<c>organFiscalCompetent</c>).</summary>
    public string? FiscalAuthority { get; init; }

    /// <summary>The registration status text (<c>stare_inregistrare</c>, e.g. "INREGISTRAT din data ...").</summary>
    public string? RegistrationStatus { get; init; }

    /// <summary>The registration date (<c>data_inregistrare</c>), parsed from ANAF's <c>yyyy-MM-dd</c> string.</summary>
    public DateOnly? RegistrationDate { get; init; }

    /// <summary>An associated administrative act (<c>act</c>), when present.</summary>
    public string? Act { get; init; }

    /// <summary>Whether the company is currently registered for VAT (<c>scpTVA</c>).</summary>
    public bool IsVatPayer { get; init; }

    /// <summary>Whether the company is currently flagged inactive (<c>statusInactivi</c>).</summary>
    public bool IsInactive { get; init; }

    /// <summary>
    ///     Whether the company uses the VAT cash-accounting scheme (<c>TVA la încasare</c>, <c>statusTvaIncasare</c>):
    ///     VAT becomes chargeable when payment is received rather than when the invoice is issued.
    /// </summary>
    public bool UsesVatCashAccounting { get; init; }

    /// <summary>Whether the company uses split VAT (<c>statusSplitTVA</c>).</summary>
    public bool UsesSplitVat { get; init; }

    /// <summary>Whether the company is enrolled in RO e-Factura (<c>statusRO_e_Factura</c>).</summary>
    public bool UsesEInvoice { get; init; }

    /// <summary>The registered-office (sediu social) address, with location codes kept raw for later mapping.</summary>
    public AnafVatCheckAddress? RegisteredOfficeAddress { get; init; }

    /// <summary>The fiscal-domicile (domiciliu fiscal) address, with location codes kept raw for later mapping.</summary>
    public AnafVatCheckAddress? FiscalDomicileAddress { get; init; }

    /// <summary>The VAT-registration periods (<c>perioade_TVA</c>).</summary>
    public IReadOnlyList<AnafVatCheckPeriod> VatPeriods { get; init; } = [];
}