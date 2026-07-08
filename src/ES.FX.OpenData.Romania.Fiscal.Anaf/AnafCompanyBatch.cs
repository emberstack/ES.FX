using JetBrains.Annotations;

namespace ES.FX.OpenData.Romania.Fiscal.Anaf;

/// <summary>The result of a batch ANAF lookup: the companies found, and the CUIs ANAF did not recognize.</summary>
[PublicAPI]
public sealed record AnafCompanyBatch
{
    /// <summary>The companies ANAF returned.</summary>
    public required IReadOnlyList<AnafCompany> Found { get; init; }

    /// <summary>The requested CUIs ANAF reported as not found.</summary>
    public required IReadOnlyList<long> NotFound { get; init; }
}
