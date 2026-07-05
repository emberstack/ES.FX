using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Configuration.OpenTelemetry;

namespace ES.FX.Ignite.NousResearch.HermesAgent.Configuration;

/// <summary>
///     Provides the settings for the Hermes Agent API client Spark.
/// </summary>
public class HermesAgentClientSparkSettings
{
    /// <summary>
    ///     <inheritdoc cref="HealthCheckSettings" />
    /// </summary>
    public HealthCheckSettings HealthChecks { get; set; } = new();

    /// <summary>
    ///     <inheritdoc cref="TracingSettings" />
    /// </summary>
    public TracingSettings Tracing { get; set; } = new();
}
