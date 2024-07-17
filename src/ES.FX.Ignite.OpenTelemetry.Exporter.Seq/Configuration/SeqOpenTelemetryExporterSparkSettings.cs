namespace ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Configuration;

/// <summary>
///     Provides the settings for connecting to Seq
/// </summary>
public class SeqOpenTelemetryExporterSparkSettings
{
    /// <summary>
    ///     Gets or sets a boolean value that indicates whether the Seq is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    ///     Gets or sets a boolean value that indicates whether the OTLP exporter for logs is enabled.
    /// </summary>
    public bool LogExporterEnabled { get; set; } = true;


    /// <summary>
    ///     Gets or sets a boolean value that indicates whether the OTLP exporter for traces is enabled.
    /// </summary>
    public bool TracesExporterEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a boolean value that indicates whether the health checks are enabled.
    /// </summary>
    public bool HealthChecksEnabled { get; set; } = true;
}