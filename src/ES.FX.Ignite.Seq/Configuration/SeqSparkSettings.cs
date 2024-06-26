﻿namespace ES.FX.Ignite.Seq.Configuration;

/// <summary>
///     Provides the settings for connecting to Seq
/// </summary>
public class SeqSparkSettings
{
    /// <summary>
    ///     Gets or sets a boolean value that indicates whether the Seq is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

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