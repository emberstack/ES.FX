using ES.FX.Ignite.Spark.Configuration.OpenTelemetry;

namespace ES.FX.Ignite.Configuration;

/// <summary>
///     Settings for HTTP client
/// </summary>
public class HttpClientSettings
{
    /// <summary>
    ///     Gets or sets a value indicating whether the standard resilience handler is enabled
    /// </summary>
    public bool StandardResilienceHandlerEnabled { get; set; } = true;

    /// <summary>
    ///     <inheritdoc cref="TracingSettings" />
    /// </summary>
    public TracingSettings Tracing { get; set; } = new();

    /// <summary>
    ///     <inheritdoc cref="MetricsSettings" />
    /// </summary>
    public MetricsSettings Metrics { get; set; } = new();
}