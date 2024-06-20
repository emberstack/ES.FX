using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.OpenTelemetry.Exporter.Seq;

/// <summary>
///     <see cref="SeqOpenTelemetryExporterSpark" /> definition
/// </summary>
public static class SeqOpenTelemetryExporterSpark
{
    /// <summary>
    ///     Spark name
    /// </summary>
    public const string Name = "SeqOpenTelemetryExporter";

    /// <summary>
    ///     The default configuration section path
    /// </summary>
    public const string ConfigurationSectionPath =
        $"{IgniteConfigurationSections.Ignite}:{nameof(OpenTelemetry)}:{nameof(Exporter)}:{nameof(Seq)}";
}