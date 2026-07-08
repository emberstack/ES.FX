using JetBrains.Annotations;

namespace ES.FX.OpenData;

/// <summary>Controls when registered datasets are materialized relative to host startup.</summary>
[PublicAPI]
public enum OpenDataWarmupMode
{
    /// <summary>Warm datasets on a background task after the host starts (default). Does not delay startup.</summary>
    Background,

    /// <summary>Warm datasets synchronously during host startup, before it accepts traffic.</summary>
    Blocking,

    /// <summary>Do not warm; datasets materialize lazily on first access.</summary>
    None
}
