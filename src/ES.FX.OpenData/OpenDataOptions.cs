using JetBrains.Annotations;

namespace ES.FX.OpenData;

/// <summary>Options for the OpenData core, configured via <c>AddOpenData(o =&gt; …)</c>.</summary>
[PublicAPI]
public sealed class OpenDataOptions
{
    /// <summary>How registered datasets are warmed relative to host startup. Defaults to <see cref="OpenDataWarmupMode.Background" />.</summary>
    public OpenDataWarmupMode WarmupMode { get; set; } = OpenDataWarmupMode.Background;
}
