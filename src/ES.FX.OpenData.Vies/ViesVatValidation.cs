using JetBrains.Annotations;

namespace ES.FX.OpenData.Vies;

/// <summary>The result of a VIES VAT-number validation.</summary>
[PublicAPI]
public sealed record ViesVatValidation
{
    /// <summary>The validation outcome.</summary>
    public required ViesValidationStatus Status { get; init; }

    /// <summary>The two-letter member-state country code that was checked.</summary>
    public required string CountryCode { get; init; }

    /// <summary>The VAT number that was checked.</summary>
    public required string VatNumber { get; init; }

    /// <summary>The timestamp VIES stamped on the response.</summary>
    public DateTimeOffset RequestDate { get; init; }

    /// <summary>The registered trader name, when VIES returned one (only for a valid number that discloses it).</summary>
    public string? Name { get; init; }

    /// <summary>The registered trader address, when VIES returned one.</summary>
    public string? Address { get; init; }
}
