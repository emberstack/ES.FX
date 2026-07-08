using JetBrains.Annotations;

namespace ES.FX.OpenData;

/// <summary>
///     A per-package registration record contributed (via <c>TryAddEnumerable</c>) by every dataset's
///     <c>Add…</c> method. Each package ships its own distinct implementation type so the enumerable is not
///     de-duplicated. Powers <see cref="IOpenData.Datasets" /> and the startup warmup service.
/// </summary>
[PublicAPI]
public interface IOpenDatasetRegistration
{
    /// <summary>The dataset's descriptor.</summary>
    OpenDatasetInfo Info { get; }

    /// <summary>Resolves the dataset singleton from the provider (used to warm it on startup).</summary>
    IOpenDataset Resolve(IServiceProvider provider);
}
