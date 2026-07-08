using JetBrains.Annotations;

namespace ES.FX.OpenData.Romania.Fiscal.Anaf;

/// <summary>A company record from the ANAF VAT-payer registry.</summary>
[PublicAPI]
public sealed record AnafCompany
{
    /// <summary>The fiscal identification code (CUI/CIF).</summary>
    public required long Cui { get; init; }

    /// <summary>The registered company name (<c>denumire</c>).</summary>
    public required string Name { get; init; }

    /// <summary>The trade-register number (<c>nrRegCom</c>), if any.</summary>
    public string? RegistrationNumber { get; init; }

    /// <summary>The registered address (<c>adresa</c>), if any.</summary>
    public string? Address { get; init; }

    /// <summary>The registered phone number (<c>telefon</c>), if any.</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>Whether the company is currently registered for VAT (<c>scpTVA</c>).</summary>
    public bool IsVatPayer { get; init; }

    /// <summary>Whether the company is currently flagged inactive (<c>statusInactivi</c>).</summary>
    public bool IsInactive { get; init; }
}
