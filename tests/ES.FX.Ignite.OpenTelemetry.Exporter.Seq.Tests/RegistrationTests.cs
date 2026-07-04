using System.Reflection;
using ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Configuration;
using ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Hosting;
using ES.FX.Ignite.Spark.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;

namespace ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Tests;

/// <summary>
///     Deterministic (no-Docker) coverage for the Seq OpenTelemetry exporter Spark registration logic.
///     These exercise the DI/registration branches and the private <c>ConfigureOtlpExporterOptions</c>
///     logic by resolving the configured <see cref="OpenTelemetryLoggerOptions" /> processor factories and
///     the configured tracer processors and inspecting their observable effects.
/// </summary>
public class RegistrationTests
{
    private static HostApplicationBuilder CreateBuilder() =>
        Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [] });

    /// <summary>
    ///     Resolves the configured <see cref="OpenTelemetryLoggerOptions" /> and returns its registered
    ///     processor factories (reachable public field on the OTel type in 1.16.x).
    /// </summary>
    private static List<Func<IServiceProvider, BaseProcessor<LogRecord>>> GetLoggerProcessorFactories(
        IServiceProvider sp)
    {
        var loggerOptions = sp.GetRequiredService<IOptions<OpenTelemetryLoggerOptions>>().Value;
        var field = typeof(OpenTelemetryLoggerOptions).GetField("ProcessorFactories",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        return (List<Func<IServiceProvider, BaseProcessor<LogRecord>>>)field.GetValue(loggerOptions)!;
    }

    // ------------------------------------------------------------------
    // Disabled (default) early-return branch
    // ------------------------------------------------------------------

    [Fact]
    public void Disabled_ByDefault_RegistersNoLogProcessor_NoHealthCheck_ButBindsSettingsAndOptions()
    {
        var builder = CreateBuilder();

        // Enabled defaults to false; provide an endpoint/health url to prove they are still ignored.
        builder.IgniteSeqOpenTelemetryExporter(configureOptions: options =>
        {
            options.IngestionEndpoint = "http://seq:5341";
            options.HealthUrl = "http://seq:5341/health";
        });

        using var host = builder.Build();
        var sp = host.Services;

        // Keyed settings ARE bound even when disabled.
        var settings = sp.GetRequiredKeyedService<SeqOpenTelemetryExporterSparkSettings>(null);
        Assert.False(settings.Enabled);

        // Options ARE bound even when disabled.
        var options = sp.GetRequiredService<IOptionsMonitor<SeqOpenTelemetryExporterSparkOptions>>()
            .Get(Options.DefaultName);
        Assert.Equal("http://seq:5341", options.IngestionEndpoint);

        // No log processor factory registered.
        Assert.Empty(GetLoggerProcessorFactories(sp));

        // No health check registered.
        var healthCheckOptions = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        Assert.DoesNotContain(healthCheckOptions.Registrations,
            r => r.Name.StartsWith(SeqOpenTelemetryExporterSpark.Name));
    }

    // ------------------------------------------------------------------
    // Health check registration gating (HealthUrl present/absent)
    // ------------------------------------------------------------------

    [Fact]
    public void Enabled_WithHealthUrl_RegistersHealthCheckUnderExpectedName()
    {
        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter(
            configureSettings: s => s.Enabled = true,
            configureOptions: o =>
            {
                o.IngestionEndpoint = "http://seq:5341";
                o.HealthUrl = "http://seq:5341/health";
            });

        using var host = builder.Build();
        var registrations = host.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations;

        Assert.Contains(registrations, r => r.Name == SeqOpenTelemetryExporterSpark.Name);
    }

    [Fact]
    public void Enabled_WithHealthUrl_AndName_RegistersHealthCheckWithBracketedName()
    {
        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter("primary",
            s => s.Enabled = true,
            o =>
            {
                o.IngestionEndpoint = "http://seq:5341";
                o.HealthUrl = "http://seq:5341/health";
            });

        using var host = builder.Build();
        var registrations = host.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations;

        Assert.Contains(registrations, r => r.Name == $"{SeqOpenTelemetryExporterSpark.Name}[primary]");
    }

    [Fact]
    public void Enabled_WithoutHealthUrl_DoesNotRegisterHealthCheck()
    {
        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter(
            configureSettings: s => s.Enabled = true,
            configureOptions: o =>
            {
                o.IngestionEndpoint = "http://seq:5341";
                // No HealthUrl -> skip registration branch.
            });

        using var host = builder.Build();
        var registrations = host.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations;

        Assert.DoesNotContain(registrations, r => r.Name.StartsWith(SeqOpenTelemetryExporterSpark.Name));
    }

    [Fact]
    public void Enabled_WithHealthUrl_ButHealthChecksDisabled_DoesNotRegisterHealthCheck()
    {
        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter(
            configureSettings: s =>
            {
                s.Enabled = true;
                s.HealthChecks.Enabled = false;
            },
            configureOptions: o =>
            {
                o.IngestionEndpoint = "http://seq:5341";
                o.HealthUrl = "http://seq:5341/health";
            });

        using var host = builder.Build();
        var registrations = host.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations;

        Assert.DoesNotContain(registrations, r => r.Name.StartsWith(SeqOpenTelemetryExporterSpark.Name));
    }

    // ------------------------------------------------------------------
    // ConfigureOtlpExporterOptions: ApiKey header injection
    // ------------------------------------------------------------------

    [Fact]
    public void ApiKey_Set_InjectsSeqApiKeyHeader_OnLogExporter()
    {
        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter(
            configureSettings: s => s.Enabled = true,
            configureOptions: o =>
            {
                o.IngestionEndpoint = "http://seq:5341";
                o.ApiKey = "abc123";
            });

        using var host = builder.Build();
        var sp = host.Services;

        // Run the log processor factory so ConfigureOtlpExporterOptions mutates OtlpLogExporter.
        var factory = Assert.Single(GetLoggerProcessorFactories(sp));
        using var processor = factory(sp);

        var options = sp.GetRequiredService<IOptionsMonitor<SeqOpenTelemetryExporterSparkOptions>>()
            .Get(Options.DefaultName);
        Assert.Equal("X-Seq-ApiKey=abc123", options.OtlpLogExporter.Headers);
    }

    [Fact]
    public void ApiKey_Set_WithExistingHeaders_AppendsSeqApiKeyHeader()
    {
        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter(
            configureSettings: s => s.Enabled = true,
            configureOptions: o =>
            {
                o.IngestionEndpoint = "http://seq:5341";
                o.ApiKey = "abc123";
                o.OtlpLogExporter.Headers = "X-Existing=1";
            });

        using var host = builder.Build();
        var sp = host.Services;

        var factory = Assert.Single(GetLoggerProcessorFactories(sp));
        using var processor = factory(sp);

        var options = sp.GetRequiredService<IOptionsMonitor<SeqOpenTelemetryExporterSparkOptions>>()
            .Get(Options.DefaultName);
        Assert.Equal("X-Existing=1,X-Seq-ApiKey=abc123", options.OtlpLogExporter.Headers);
    }

    [Fact]
    public void ApiKey_NotSet_DoesNotAddHeader()
    {
        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter(
            configureSettings: s => s.Enabled = true,
            configureOptions: o => o.IngestionEndpoint = "http://seq:5341");

        using var host = builder.Build();
        var sp = host.Services;

        var factory = Assert.Single(GetLoggerProcessorFactories(sp));
        using var processor = factory(sp);

        var options = sp.GetRequiredService<IOptionsMonitor<SeqOpenTelemetryExporterSparkOptions>>()
            .Get(Options.DefaultName);
        Assert.True(string.IsNullOrEmpty(options.OtlpLogExporter.Headers));
    }

    // ------------------------------------------------------------------
    // ConfigureOtlpExporterOptions: endpoint / protocol handling
    // ------------------------------------------------------------------

    [Fact]
    public void HttpProtobuf_AppendsLogsIngestPathSuffix()
    {
        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter(
            configureSettings: s => s.Enabled = true,
            configureOptions: o =>
            {
                o.IngestionEndpoint = "http://seq:5341/";
                o.OtlpProtocol = OtlpExportProtocol.HttpProtobuf;
            });

        using var host = builder.Build();
        var sp = host.Services;

        var factory = Assert.Single(GetLoggerProcessorFactories(sp));
        using var processor = factory(sp);

        var options = sp.GetRequiredService<IOptionsMonitor<SeqOpenTelemetryExporterSparkOptions>>()
            .Get(Options.DefaultName);
        // Trailing slash trimmed, then /ingest/otlp/v1/logs appended.
        Assert.Equal(new Uri("http://seq:5341/ingest/otlp/v1/logs"), options.OtlpLogExporter.Endpoint);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, options.OtlpLogExporter.Protocol);
    }

    [Fact]
    public void Grpc_UsesEndpointVerbatim_NoSuffix()
    {
        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter(
            configureSettings: s => s.Enabled = true,
            configureOptions: o =>
            {
                o.IngestionEndpoint = "http://seq:5341";
                o.OtlpProtocol = OtlpExportProtocol.Grpc;
            });

        using var host = builder.Build();
        var sp = host.Services;

        var factory = Assert.Single(GetLoggerProcessorFactories(sp));
        using var processor = factory(sp);

        var options = sp.GetRequiredService<IOptionsMonitor<SeqOpenTelemetryExporterSparkOptions>>()
            .Get(Options.DefaultName);
        Assert.Equal(new Uri("http://seq:5341"), options.OtlpLogExporter.Endpoint);
        Assert.Equal(OtlpExportProtocol.Grpc, options.OtlpLogExporter.Protocol);
    }

    [Fact]
    public void EmptyIngestionEndpoint_LeavesDefaultEndpoint()
    {
        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter(
            configureSettings: s => s.Enabled = true,
            configureOptions: o => o.OtlpProtocol = OtlpExportProtocol.HttpProtobuf);

        using var host = builder.Build();
        var sp = host.Services;

        var factory = Assert.Single(GetLoggerProcessorFactories(sp));
        using var processor = factory(sp);

        var options = sp.GetRequiredService<IOptionsMonitor<SeqOpenTelemetryExporterSparkOptions>>()
            .Get(Options.DefaultName);
        // IngestionEndpoint empty -> Endpoint left untouched: no /ingest/otlp/v1/logs suffix applied.
        // With Protocol=HttpProtobuf the OTLP exporter resolves its own protocol default (localhost:4318).
        Assert.Equal(new Uri("http://localhost:4318/"), options.OtlpLogExporter.Endpoint);
        Assert.DoesNotContain("ingest/otlp", options.OtlpLogExporter.Endpoint.ToString());
    }

    // ------------------------------------------------------------------
    // ExportProcessorType Batch vs Simple selection (logs)
    // ------------------------------------------------------------------

    [Fact]
    public void LogProcessor_DefaultsToBatch()
    {
        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter(
            configureSettings: s => s.Enabled = true,
            configureOptions: o => o.IngestionEndpoint = "http://seq:5341");

        using var host = builder.Build();
        var sp = host.Services;

        var factory = Assert.Single(GetLoggerProcessorFactories(sp));
        using var processor = factory(sp);

        Assert.IsType<BatchLogRecordExportProcessor>(processor);
    }

    [Fact]
    public void LogProcessor_SimpleExportProcessorType_ProducesSimpleProcessor()
    {
        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter(
            configureSettings: s => s.Enabled = true,
            configureOptions: o =>
            {
                o.IngestionEndpoint = "http://seq:5341";
                o.OtlpLogExporter.ExportProcessorType = ExportProcessorType.Simple;
            });

        using var host = builder.Build();
        var sp = host.Services;

        var factory = Assert.Single(GetLoggerProcessorFactories(sp));
        using var processor = factory(sp);

        Assert.IsType<SimpleLogRecordExportProcessor>(processor);
    }

    // ------------------------------------------------------------------
    // LogExporterEnabled=false branch
    // ------------------------------------------------------------------

    [Fact]
    public void LogExporterDisabled_RegistersNoLogProcessor()
    {
        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter(
            configureSettings: s =>
            {
                s.Enabled = true;
                s.LogExporterEnabled = false;
            },
            configureOptions: o => o.IngestionEndpoint = "http://seq:5341");

        using var host = builder.Build();

        Assert.Empty(GetLoggerProcessorFactories(host.Services));
    }

    // ------------------------------------------------------------------
    // Named/keyed instance resolution + duplicate guard
    // ------------------------------------------------------------------

    [Fact]
    public void MultipleNamedInstances_RegisterIndependentSettingsAndOptions()
    {
        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter("alpha",
            s => s.Enabled = true,
            o => o.IngestionEndpoint = "http://alpha:5341");
        builder.IgniteSeqOpenTelemetryExporter("beta",
            s => s.Enabled = false,
            o => o.IngestionEndpoint = "http://beta:5341");

        using var host = builder.Build();
        var sp = host.Services;

        var alphaSettings = sp.GetRequiredKeyedService<SeqOpenTelemetryExporterSparkSettings>("alpha");
        var betaSettings = sp.GetRequiredKeyedService<SeqOpenTelemetryExporterSparkSettings>("beta");
        Assert.True(alphaSettings.Enabled);
        Assert.False(betaSettings.Enabled);

        var monitor = sp.GetRequiredService<IOptionsMonitor<SeqOpenTelemetryExporterSparkOptions>>();
        Assert.Equal("http://alpha:5341", monitor.Get("alpha").IngestionEndpoint);
        Assert.Equal("http://beta:5341", monitor.Get("beta").IngestionEndpoint);
    }

    [Fact]
    public void DuplicateName_Throws_ReconfigurationNotSupported()
    {
        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter("dup", s => s.Enabled = true);

        Assert.Throws<ReconfigurationNotSupportedException>(() =>
            builder.IgniteSeqOpenTelemetryExporter("dup", s => s.Enabled = true));
    }

    // ------------------------------------------------------------------
    // Health-check behavior (URI source, FailureStatus, tags, timeout, and
    // exception/status mapping) is covered in HealthCheckFactoryTests, which
    // drives the Spark-registered HealthCheckRegistration.Factory rather than a
    // hand-rolled HttpGetHealthCheck. The former self-authored test here only
    // exercised the HealthChecks.Http Addition and asserted nothing about this
    // Spark's ConfigureObservability wiring, so it was removed.
    // ------------------------------------------------------------------
}