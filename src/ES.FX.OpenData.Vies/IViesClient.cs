using JetBrains.Annotations;

namespace ES.FX.OpenData.Vies;

/// <summary>A typed client for the EU VIES VAT-number validation service.</summary>
[PublicAPI]
public interface IViesClient
{
    /// <summary>
    ///     Validates a VAT number for a member state. Returns a <see cref="ViesVatValidation" /> whose
    ///     <see cref="ViesVatValidation.Status" /> is valid, invalid, or member-state-unavailable. Only genuine
    ///     faults throw <see cref="ViesApiException" /> (or <see cref="ArgumentException" /> for rejected input).
    /// </summary>
    /// <param name="countryCode">The two-letter member-state code (e.g. <c>"RO"</c>).</param>
    /// <param name="vatNumber">The VAT number without the country prefix.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<ViesVatValidation> ValidateAsync(string countryCode, string vatNumber,
        CancellationToken cancellationToken = default);
}