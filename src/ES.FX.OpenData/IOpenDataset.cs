using JetBrains.Annotations;

namespace ES.FX.OpenData;

/// <summary>
///     Marker interface implemented by every OpenData dataset. Provides its <see cref="OpenDatasetInfo" /> and a
///     way to force eager materialization (used by the warmup hosted service).
/// </summary>
[PublicAPI]
public interface IOpenDataset
{
    /// <summary>Identity, edition, and provenance of this dataset.</summary>
    OpenDatasetInfo Info { get; }

    /// <summary>
    ///     Forces the dataset's backing store to be parsed and indexed now, instead of lazily on first access.
    ///     Safe to call multiple times; subsequent calls are no-ops.
    /// </summary>
    void EnsureLoaded();
}
