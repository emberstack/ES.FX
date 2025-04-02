using ES.FX.Ignite.Spark.Configuration.OpenTelemetry;

namespace ES.FX.Ignite.Configuration.Runtime;

public class RuntimeSettings
{
    /// <summary>
    ///     <inheritdoc cref="MetricsSettings" />
    /// </summary>
    public MetricsSettings Metrics { get; set; } = new() { Enabled = false };
}