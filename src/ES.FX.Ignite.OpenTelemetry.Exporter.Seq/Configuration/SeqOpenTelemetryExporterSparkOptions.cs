using OpenTelemetry.Exporter;

namespace ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Configuration;

/// <summary>
///     Provides the options for connecting to Seq
/// </summary>
public class SeqOpenTelemetryExporterSparkOptions
{
    /// <summary>
    ///     The Seq ingestion endpoint.
    /// </summary>
    public string? IngestionEndpoint { get; set; }


    /// <summary>
    ///     Sets the OTLP protocol to use. Overrides the protocol in <see cref="OtlpLogExporter" /> and
    ///     <see cref="OtlpTraceExporter" />
    /// </summary>
    public OtlpExportProtocol OtlpProtocol { get; set; } = OtlpExportProtocol.HttpProtobuf;


    /// <summary>
    ///     The Seq server health URL.
    /// </summary>
    public string? HealthUrl { get; set; }


    /// <summary>
    ///     Gets or sets the Seq <i>API key</i>.
    /// </summary>
    public string? ApiKey { get; set; }


    /// <summary>
    ///     Gets OTLP exporter options for logs.
    /// </summary>
    public OtlpExporterOptions OtlpLogExporter { get; } = new();

    /// <summary>
    ///     Gets OTLP exporter options for traces.
    /// </summary>
    public OtlpExporterOptions OtlpTraceExporter { get; } = new();
}