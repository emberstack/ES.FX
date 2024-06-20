using ES.FX.Ignite.Seq.Configuration;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Microsoft.Extensions.Diagnostics.HealthChecks;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using BatchActivityExportProcessor = OpenTelemetry.BatchActivityExportProcessor;
using BatchLogRecordExportProcessor = OpenTelemetry.BatchLogRecordExportProcessor;
using ExportProcessorType = OpenTelemetry.ExportProcessorType;
using SimpleActivityExportProcessor = OpenTelemetry.SimpleActivityExportProcessor;
using SimpleLogRecordExportProcessor = OpenTelemetry.SimpleLogRecordExportProcessor;

namespace ES.FX.Ignite.Seq.Hosting;

[PublicAPI]
public static class SeqHostingExtensions
{
    public static void AddIgniteSeq(this IHostApplicationBuilder builder,
        string name,
        Action<SeqSparkSettings>? configureSettings = null,
        Action<SeqSparkOptions>? configureOptions = null,
        string configurationSectionPath = SeqSpark.ConfigurationSectionPath)
    {
        var configPath = SparkConfig.Path(name, configurationSectionPath);

        var settings = SparkConfig.GetSettings(builder.Configuration, configPath, configureSettings);
        builder.Services.AddKeyedSingleton(name, settings);

        var optionsBuilder = builder.Services
            .AddOptions<SeqSparkOptions>(name)
            .BindConfiguration(configPath);
        if (configureOptions is not null) optionsBuilder.Configure(configureOptions);

        if (!settings.Enabled) return;

        if (settings.LogExporterEnabled)
            builder.Services.Configure<OpenTelemetryLoggerOptions>(logging => logging.AddProcessor(
                sp =>
                {
                    var options = sp.GetRequiredService<IOptionsMonitor<SeqSparkOptions>>().Get(name);
                    ConfigureOtlpExporterOptions(options, options.OtlpLogExporter, "/ingest/otlp/v1/logs");

                    var exporter = new OtlpLogExporter(options.OtlpLogExporter);
                    return options.OtlpLogExporter.ExportProcessorType switch
                    {
                        ExportProcessorType.Batch => new BatchLogRecordExportProcessor(exporter),
                        _ => new SimpleLogRecordExportProcessor(exporter)
                    };
                }));

        if (settings.TracesExporterEnabled)
            builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddProcessor(
                sp =>
                {
                    var options = sp.GetRequiredService<IOptionsMonitor<SeqSparkOptions>>().Get(name);
                    ConfigureOtlpExporterOptions(options, options.OtlpTraceExporter, "/ingest/otlp/v1/traces");

                    var exporter = new OtlpTraceExporter(options.OtlpTraceExporter);
                    return options.OtlpTraceExporter.ExportProcessorType switch
                    {
                        ExportProcessorType.Batch => new BatchActivityExportProcessor(exporter),
                        _ => new SimpleActivityExportProcessor(exporter)
                    };
                }));


        ConfigureObservability(builder, name, settings);

        return;


        static void ConfigureOtlpExporterOptions(SeqSparkOptions options, OtlpExporterOptions exporterOptions,
            string httpProtobufSuffix)
        {
            exporterOptions.Protocol = options.OtlpProtocol;
            if (!string.IsNullOrWhiteSpace(options.IngestionEndpoint))
            {
                exporterOptions.Endpoint = new Uri(options.IngestionEndpoint);
                if (options.OtlpProtocol == OtlpExportProtocol.HttpProtobuf)
                    exporterOptions.Endpoint = new Uri($"{options.IngestionEndpoint}{httpProtobufSuffix}");
            }

            if (!string.IsNullOrEmpty(options.ApiKey))
                options.OtlpTraceExporter.Headers = string.IsNullOrEmpty(options.OtlpTraceExporter.Headers)
                    ? $"X-Seq-ApiKey={options.ApiKey}"
                    : $"{options.OtlpTraceExporter.Headers},X-Seq-ApiKey={options.ApiKey}";
        }
    }

    private static void ConfigureObservability(IHostApplicationBuilder builder, string name, SeqSparkSettings settings)
    {
        if (settings.HealthChecksEnabled)
        {
            var healthCheckName = $"{SeqSpark.Name}.{name.Trim()}";
            builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(healthCheckName, sp =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<SeqSparkOptions>>().Get(name);
                return new HttpGetHealthCheck(new HttpGetHealthCheckOptions
                {
                    Uri = options.HealthUrl ?? string.Empty
                });
            }, default, [SeqSpark.Name], default));
        }
    }
}