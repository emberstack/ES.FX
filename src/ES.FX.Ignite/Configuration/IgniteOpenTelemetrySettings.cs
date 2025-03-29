namespace ES.FX.Ignite.Configuration;

/// <summary>
///     Settings for OpenTelemetry
/// </summary>
public class IgniteOpenTelemetrySettings
{
    /// <summary>
    ///     Gets or sets a value indicating whether OpenTelemetry is enabled. If set to false, all OpenTelemetry features will
    ///     be disabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether logging is enabled
    /// </summary>
    public bool LoggingEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the formatted message should be included in the log
    /// </summary>
    public bool LoggingIncludeFormattedMessage { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the scope should be included in the log
    /// </summary>
    public bool LoggingIncludeScopes { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the OpenTelemetry Protocol (OTLP) exporter is enabled
    /// </summary>
    public bool UseOtlpExporter { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the AzureMonitor exporter is enabled
    /// </summary>
    public bool UseAzureMonitor { get; set; } = false;
}