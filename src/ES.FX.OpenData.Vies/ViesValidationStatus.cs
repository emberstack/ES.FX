using JetBrains.Annotations;

namespace ES.FX.OpenData.Vies;

/// <summary>The outcome of a VIES VAT-number validation. A tri-state value — not every non-valid result is a fault.</summary>
[PublicAPI]
public enum ViesValidationStatus
{
    /// <summary>Validation outcome not determined (default). The client never returns this value.</summary>
    Unknown = 0,

    /// <summary>The VAT number is valid and registered for intra-EU transactions.</summary>
    Valid = 1,

    /// <summary>The VAT number is not valid.</summary>
    Invalid = 2,

    /// <summary>
    ///     The member state's system was unavailable (VIES <c>MS_UNAVAILABLE</c>) — a routine, expected
    ///     condition (e.g. nightly maintenance), not a fault. Retry later.
    /// </summary>
    MemberStateUnavailable = 3
}