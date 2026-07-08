using JetBrains.Annotations;

namespace ES.FX.OpenData;

/// <summary>
///     The optional fluent entry point ("hub") over all registered OpenData datasets. Injecting the specific
///     dataset interface (e.g. <c>ICountriesDataset</c>) directly is the primary, recommended path; the hub
///     exists for cross-dataset work and diagnostics. Each dataset package contributes a fluent accessor as a
///     C# extension member on this interface (e.g. <c>openData.Countries</c>).
/// </summary>
[PublicAPI]
public interface IOpenData
{
    /// <summary>
    ///     Resolves a registered dataset by its interface type.
    /// </summary>
    /// <typeparam name="T">The dataset interface (e.g. <c>ICountriesDataset</c>).</typeparam>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when no dataset of type <typeparamref name="T" /> is registered — i.e. the package was not
    ///     installed or its <c>Add…</c> method was not called.
    /// </exception>
    T GetDataset<T>() where T : class, IOpenDataset;

    /// <summary>The <see cref="OpenDatasetInfo" /> of every registered dataset, for diagnostics and audit.</summary>
    IReadOnlyList<OpenDatasetInfo> Datasets { get; }
}
