namespace ES.FX.Ignite.Hosting.Configuration;

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
    ///     Gets or sets a value indicating whether ASP.NET Core metrics are enabled
    /// </summary>
    public bool AspNetCoreMetricsEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether ASP.NET Core tracing is enabled
    /// </summary>
    public bool AspNetCoreTracingEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether HttpClient metrics are enabled
    /// </summary>
    public bool HttpClientMetricsEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether HttpClient tracing is enabled
    /// </summary>
    public bool HttpClientTracingEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether Runtime metrics are enabled
    /// </summary>
    public bool RuntimeMetricsEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the OpenTelemetry Protocol (OTLP) exporter is enabled
    /// </summary>
    public bool OtlpExporterEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the AzureMonitor exporter is enabled
    /// </summary>
    public bool AzureMonitorExporterEnabled { get; set; } = false;
}