using System.Diagnostics;
using System.Diagnostics.Metrics;
using ES.FX.Ignite.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace ES.FX.Ignite.Tests;

/// <summary>
///     Behavioral coverage for the OpenTelemetry wiring performed by
///     <see cref="IgniteHostingExtensions.Ignite(IHostApplicationBuilder, System.Action{ES.FX.Ignite.Configuration.IgniteSettings}?, string)" />.
///     Unlike a presence check on the MeterProvider/TracerProvider (which exist regardless of what
///     instrumentation or exporters are wired inside the builder lambdas), these tests observe the REAL
///     effects: which meters/activity sources the built providers actually subscribe to, and whether the
///     OTLP exporter registration is present. This detects mutations that drop an
///     <c>AddXInstrumentation()</c> call, ignore an <c>.Enabled</c> gate, or change the OTLP endpoint guard.
/// </summary>
public class IgniteOpenTelemetryBehaviorTests
{
    private const string RuntimeMeter = "System.Runtime";
    private const string HttpMeter = "System.Net.Http";
    private const string AspNetCoreMeter = "Microsoft.AspNetCore.Hosting";
    private const string HttpActivitySource = "System.Net.Http";
    private const string AspNetCoreActivitySource = "Microsoft.AspNetCore";

    private const string OtlpRegistrationTypeName = "UseOtlpExporterRegistration";

    private static IHostApplicationBuilder CreateBuilder(IEnumerable<KeyValuePair<string, string?>> configuration)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(configuration);
        return builder;
    }

    /// <summary>
    ///     Builds the configured host, attaches an in-memory metric reader, resolves the real
    ///     <see cref="MeterProvider" />, publishes a measurement on each candidate meter, flushes, and
    ///     returns the distinct set of meter names the provider actually collected. A meter only appears
    ///     here if the provider subscribed to it (i.e. the corresponding <c>AddXInstrumentation()</c> ran).
    /// </summary>
    private static HashSet<string> CollectMeters(
        IEnumerable<KeyValuePair<string, string?>> configuration,
        params string[] meterNames)
    {
        var exporter = new CapturingMetricExporter();
        var builder = CreateBuilder(configuration);
        builder.Ignite();
        builder.Services.ConfigureOpenTelemetryMeterProvider(mpb =>
            mpb.AddReader(new PeriodicExportingMetricReader(exporter)));

        using var provider = builder.Services.BuildServiceProvider();
        var meterProvider = provider.GetRequiredService<MeterProvider>();

        var meters = new List<Meter>();
        try
        {
            foreach (var name in meterNames)
            {
                var meter = new Meter(name);
                meters.Add(meter);
                meter.CreateCounter<long>("es.fx.test.counter").Add(1);
            }

            // The runtime instrumentation observes runtime state directly (no explicit publish needed);
            // ForceFlush triggers collection of every subscribed meter.
            meterProvider.ForceFlush();
        }
        finally
        {
            foreach (var meter in meters) meter.Dispose();
        }

        return exporter.MeterNames.ToHashSet();
    }

    /// <summary>
    ///     Same idea as <see cref="CollectMeters" /> but for tracing: starts an activity on each candidate
    ///     <see cref="ActivitySource" /> and returns the set of source names the provider actually exported.
    /// </summary>
    private static HashSet<string> CollectActivitySources(
        IEnumerable<KeyValuePair<string, string?>> configuration,
        params string[] sourceNames)
    {
        var exporter = new CapturingActivityExporter();
        var builder = CreateBuilder(configuration);
        builder.Ignite();
        builder.Services.ConfigureOpenTelemetryTracerProvider(tpb =>
            tpb.AddProcessor(new SimpleActivityExportProcessor(exporter)));

        using var provider = builder.Services.BuildServiceProvider();
        var tracerProvider = provider.GetRequiredService<TracerProvider>();

        var sources = new List<ActivitySource>();
        try
        {
            foreach (var name in sourceNames)
            {
                var source = new ActivitySource(name);
                sources.Add(source);
                // If the provider subscribed to this source, StartActivity returns a real, sampled
                // Activity which is exported on Stop.
                using (source.StartActivity("es.fx.test.activity"))
                {
                }
            }

            tracerProvider.ForceFlush();
        }
        finally
        {
            foreach (var source in sources) source.Dispose();
        }

        return exporter.Sources.ToHashSet();
    }

    private static bool HasOtlpExporterRegistration(IEnumerable<KeyValuePair<string, string?>> configuration)
    {
        var builder = CreateBuilder(configuration);
        builder.Ignite();
        return builder.Services.Any(descriptor =>
            (descriptor.ImplementationInstance?.GetType().FullName ??
             descriptor.ImplementationType?.FullName ??
             descriptor.ServiceType.FullName ?? string.Empty)
            .Contains(OtlpRegistrationTypeName, StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------------------------
    // Runtime instrumentation (metrics). Gated on Runtime.Metrics.Enabled, which defaults to false.
    // Catches: dropping AddRuntimeInstrumentation(); always-adding it; ignoring the .Enabled gate.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RuntimeMetricsEnabled_ProviderCollectsRuntimeMeter()
    {
        var meters = CollectMeters(new Dictionary<string, string?>
        {
            ["Ignite:Settings:Runtime:Metrics:Enabled"] = "true"
        }, RuntimeMeter);

        Assert.Contains(RuntimeMeter, meters);
    }

    [Fact]
    public void RuntimeMetricsDisabled_ProviderDoesNotCollectRuntimeMeter()
    {
        // Default is disabled; assert the runtime meter is NOT collected. If AddRuntimeInstrumentation()
        // were called unconditionally (ignoring the gate), System.Runtime would appear here.
        var meters = CollectMeters(new Dictionary<string, string?>
        {
            ["Ignite:Settings:Runtime:Metrics:Enabled"] = "false"
        }, RuntimeMeter);

        Assert.DoesNotContain(RuntimeMeter, meters);
    }

    // ---------------------------------------------------------------------------------------------
    // HttpClient instrumentation (metrics + tracing), each on its own .Enabled gate.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void HttpClientMetricsEnabled_ProviderCollectsHttpMeter()
    {
        var meters = CollectMeters(new Dictionary<string, string?>
        {
            ["Ignite:Settings:HttpClient:Metrics:Enabled"] = "true"
        }, HttpMeter);

        Assert.Contains(HttpMeter, meters);
    }

    [Fact]
    public void HttpClientMetricsDisabled_ProviderDoesNotCollectHttpMeter()
    {
        var meters = CollectMeters(new Dictionary<string, string?>
        {
            ["Ignite:Settings:HttpClient:Metrics:Enabled"] = "false"
        }, HttpMeter);

        Assert.DoesNotContain(HttpMeter, meters);
    }

    [Fact]
    public void HttpClientTracingEnabled_ProviderSubscribesHttpActivitySource()
    {
        var sources = CollectActivitySources(new Dictionary<string, string?>
        {
            ["Ignite:Settings:HttpClient:Tracing:Enabled"] = "true"
        }, HttpActivitySource);

        Assert.Contains(HttpActivitySource, sources);
    }

    [Fact]
    public void HttpClientTracingDisabled_ProviderDoesNotSubscribeHttpActivitySource()
    {
        var sources = CollectActivitySources(new Dictionary<string, string?>
        {
            ["Ignite:Settings:HttpClient:Tracing:Enabled"] = "false"
        }, HttpActivitySource);

        Assert.DoesNotContain(HttpActivitySource, sources);
    }

    // ---------------------------------------------------------------------------------------------
    // AspNetCore instrumentation (metrics + tracing), each on its own .Enabled gate.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AspNetCoreMetricsEnabled_ProviderCollectsAspNetCoreMeter()
    {
        var meters = CollectMeters(new Dictionary<string, string?>
        {
            ["Ignite:Settings:AspNetCore:Metrics:Enabled"] = "true"
        }, AspNetCoreMeter);

        Assert.Contains(AspNetCoreMeter, meters);
    }

    [Fact]
    public void AspNetCoreMetricsDisabled_ProviderDoesNotCollectAspNetCoreMeter()
    {
        var meters = CollectMeters(new Dictionary<string, string?>
        {
            ["Ignite:Settings:AspNetCore:Metrics:Enabled"] = "false"
        }, AspNetCoreMeter);

        Assert.DoesNotContain(AspNetCoreMeter, meters);
    }

    [Fact]
    public void AspNetCoreTracingEnabled_ProviderSubscribesAspNetCoreActivitySource()
    {
        var sources = CollectActivitySources(new Dictionary<string, string?>
        {
            ["Ignite:Settings:AspNetCore:Tracing:Enabled"] = "true"
        }, AspNetCoreActivitySource);

        Assert.Contains(AspNetCoreActivitySource, sources);
    }

    [Fact]
    public void AspNetCoreTracingDisabled_ProviderDoesNotSubscribeAspNetCoreActivitySource()
    {
        var sources = CollectActivitySources(new Dictionary<string, string?>
        {
            ["Ignite:Settings:AspNetCore:Tracing:Enabled"] = "false"
        }, AspNetCoreActivitySource);

        Assert.DoesNotContain(AspNetCoreActivitySource, sources);
    }

    // ---------------------------------------------------------------------------------------------
    // Independence of the per-signal gates: turning one instrumentation off must not disable another.
    // Catches a mutation that replaces the individual gates with a single shared flag.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void OnlyHttpMetricsEnabled_CollectsHttpButNotAspNetCoreNorRuntime()
    {
        var meters = CollectMeters(new Dictionary<string, string?>
        {
            ["Ignite:Settings:Runtime:Metrics:Enabled"] = "false",
            ["Ignite:Settings:HttpClient:Metrics:Enabled"] = "true",
            ["Ignite:Settings:AspNetCore:Metrics:Enabled"] = "false"
        }, RuntimeMeter, HttpMeter, AspNetCoreMeter);

        Assert.Contains(HttpMeter, meters);
        Assert.DoesNotContain(AspNetCoreMeter, meters);
        Assert.DoesNotContain(RuntimeMeter, meters);
    }

    // ---------------------------------------------------------------------------------------------
    // OTLP exporter wiring. The exporter must be registered only when UseOtlpExporter is true AND an
    // OTEL_EXPORTER_OTLP_*_ENDPOINT is configured (Aspire-matching behavior). Catches: flipping the
    // guard (&& -> ||), dropping the IsNullOrWhiteSpace checks, removing the whole if (always export),
    // or inverting settings.OpenTelemetry.UseOtlpExporter.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void OtlpExporter_NotRegistered_WhenNoEndpointConfigured()
    {
        // No OTEL_EXPORTER_OTLP_* keys, UseOtlpExporter defaults to true. The exporter must stay OFF so
        // the app does not silently export to localhost:4317.
        Assert.False(HasOtlpExporterRegistration(new Dictionary<string, string?>()));
    }

    [Theory]
    [InlineData("OTEL_EXPORTER_OTLP_ENDPOINT")]
    [InlineData("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT")]
    [InlineData("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT")]
    [InlineData("OTEL_EXPORTER_OTLP_LOGS_ENDPOINT")]
    public void OtlpExporter_Registered_WhenAnyEndpointVariantConfigured(string endpointKey)
    {
        Assert.True(HasOtlpExporterRegistration(new Dictionary<string, string?>
        {
            [endpointKey] = "http://localhost:4317"
        }));
    }

    [Fact]
    public void OtlpExporter_NotRegistered_WhenEndpointIsWhitespace()
    {
        // The guard uses IsNullOrWhiteSpace; a whitespace endpoint must not enable the exporter.
        Assert.False(HasOtlpExporterRegistration(new Dictionary<string, string?>
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "   "
        }));
    }

    [Fact]
    public void OtlpExporter_NotRegistered_WhenUseOtlpExporterDisabled_EvenWithEndpoint()
    {
        // Endpoint present but the setting is off -> exporter must NOT be registered. Catches inverting
        // the settings.OpenTelemetry.UseOtlpExporter check.
        Assert.False(HasOtlpExporterRegistration(new Dictionary<string, string?>
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317",
            ["Ignite:Settings:OpenTelemetry:UseOtlpExporter"] = "false"
        }));
    }

    [Fact]
    public void OtlpExporter_NotRegistered_WhenOpenTelemetryMasterGateDisabled()
    {
        // With OpenTelemetry.Enabled=false the whole AddOpenTelemetry method returns early, so even a
        // configured endpoint must not wire the exporter.
        Assert.False(HasOtlpExporterRegistration(new Dictionary<string, string?>
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317",
            ["Ignite:Settings:OpenTelemetry:Enabled"] = "false"
        }));
    }

    private sealed class CapturingMetricExporter : BaseExporter<Metric>
    {
        public List<string> MeterNames { get; } = [];

        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch) MeterNames.Add(metric.MeterName);
            return ExportResult.Success;
        }
    }

    private sealed class CapturingActivityExporter : BaseExporter<Activity>
    {
        public List<string> Sources { get; } = [];

        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch) Sources.Add(activity.Source.Name);
            return ExportResult.Success;
        }
    }
}
