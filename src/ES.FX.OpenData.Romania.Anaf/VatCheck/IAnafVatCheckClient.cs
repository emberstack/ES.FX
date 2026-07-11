using JetBrains.Annotations;

namespace ES.FX.OpenData.Romania.Anaf.VatCheck;

/// <summary>A typed client for the Romanian ANAF VAT-payer registry (PlatitorTvaRest v9).</summary>
[PublicAPI]
public interface IAnafVatCheckClient
{
    /// <summary>
    ///     Looks up a single company by CUI. Returns <c>null</c> when ANAF does not recognize the CUI (an expected
    ///     outcome, not a fault). Implemented over the batch endpoint.
    /// </summary>
    /// <param name="cui">The fiscal identification code.</param>
    /// <param name="asOf">The reference date for the query; defaults to today (UTC).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<AnafCompanyVatCheckResult?> FindCompanyAsync(long cui, DateOnly? asOf = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Looks up many companies in one call. Inputs above the configured batch size are chunked into
    ///     sequential requests paced by the client's throttle, so a large batch can span several seconds.
    /// </summary>
    /// <param name="cuis">The fiscal identification codes.</param>
    /// <param name="asOf">The reference date for the query; defaults to today (UTC).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<AnafBatchCompanyVatCheckResult> FindCompaniesAsync(IReadOnlyCollection<long> cuis, DateOnly? asOf = null,
        CancellationToken cancellationToken = default);
}