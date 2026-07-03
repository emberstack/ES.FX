using ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Configuration;
using ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

namespace ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Tests;

/// <summary>
///     Deterministic (no-Docker) coverage for the traces exporter registration branch. Builds a real
///     <see cref="TracerProvider" /> so the Spark's <c>ConfigureOpenTelemetryTracerProvider</c> processor
///     factory runs, then asserts the observable effect of <c>ConfigureOtlpExporterOptions</c> on the
///     configured <see cref="SeqOpenTelemetryExporterSparkOptions.OtlpTraceExporter" />.
/// </summary>
public class TracesRegistrationTests
{
    private static HostApplicationBuilder CreateBuilder() =>
        Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [] });

    [Fact]
    public void TracesEnabled_HttpProtobuf_AppendsTracesIngestPathSuffix()
    {
        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter(
            configureSettings: s =>
            {
                s.Enabled = true;
                s.LogExporterEnabled = false; // isolate traces path
            },
            configureOptions: o =>
            {
                o.IngestionEndpoint = "http://seq:5341/";
                o.OtlpProtocol = OtlpExportProtocol.HttpProtobuf;
            });

        // Provide the tracer pipeline that Ignite would normally add so the trace processor factory runs.
        builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSource("test-source"));

        using var host = builder.Build();
        var sp = host.Services;

        // Resolving the TracerProvider forces the configure callbacks + processor factory to execute.
        var provider = sp.GetRequiredService<TracerProvider>();
        Assert.NotNull(provider);

        var options = sp.GetRequiredService<IOptionsMonitor<SeqOpenTelemetryExporterSparkOptions>>()
            .Get(Options.DefaultName);
        Assert.Equal(new Uri("http://seq:5341/ingest/otlp/v1/traces"), options.OtlpTraceExporter.Endpoint);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, options.OtlpTraceExporter.Protocol);
    }

    [Fact]
    public void TracesEnabled_ApiKey_InjectsHeaderOnTraceExporter()
    {
        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter(
            configureSettings: s =>
            {
                s.Enabled = true;
                s.LogExporterEnabled = false;
            },
            configureOptions: o =>
            {
                o.IngestionEndpoint = "http://seq:5341";
                o.ApiKey = "trace-key";
            });

        builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSource("test-source"));

        using var host = builder.Build();
        var sp = host.Services;
        _ = sp.GetRequiredService<TracerProvider>();

        var options = sp.GetRequiredService<IOptionsMonitor<SeqOpenTelemetryExporterSparkOptions>>()
            .Get(Options.DefaultName);
        Assert.Equal("X-Seq-ApiKey=trace-key", options.OtlpTraceExporter.Headers);
    }

    [Fact]
    public void TracesDisabled_LeavesTraceExporterEndpointAtDefault()
    {
        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter(
            configureSettings: s =>
            {
                s.Enabled = true;
                s.TracesExporterEnabled = false; // suppress trace processor
                s.LogExporterEnabled = false;
            },
            configureOptions: o =>
            {
                o.IngestionEndpoint = "http://seq:5341";
                o.OtlpProtocol = OtlpExportProtocol.HttpProtobuf;
            });

        builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSource("test-source"));

        using var host = builder.Build();
        var sp = host.Services;
        _ = sp.GetRequiredService<TracerProvider>();

        var options = sp.GetRequiredService<IOptionsMonitor<SeqOpenTelemetryExporterSparkOptions>>()
            .Get(Options.DefaultName);
        // Trace processor was never registered, so ConfigureOtlpExporterOptions never ran on the trace
        // exporter -> endpoint stays at the OTLP default (no /ingest/otlp/v1/traces suffix).
        Assert.Equal(new Uri("http://localhost:4317/"), options.OtlpTraceExporter.Endpoint);
    }
}
