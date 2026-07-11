using JetBrains.Annotations;

namespace ES.FX.OpenData.Romania.Anaf.VatCheck;

/// <summary>The result of a batch ANAF lookup: the companies found, and the CUIs ANAF did not recognize.</summary>
[PublicAPI]
public sealed record AnafBatchCompanyVatCheckResult
{
    /// <summary>The companies ANAF returned.</summary>
    public required IReadOnlyList<AnafCompanyVatCheckResult> Found { get; init; }

    /// <summary>The requested CUIs ANAF reported as not found.</summary>
    public required IReadOnlyList<long> NotFound { get; init; }
}