using System.Net;
using ES.FX.Additions.Microsoft.Extensions.Diagnostics.HealthChecks.Http;
using ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Tests;

/// <summary>
///     Behavioral coverage for the Spark-registered health-check factory itself
///     (<c>SeqOpenTelemetryExporterHostingExtensions.ConfigureObservability</c>). Unlike
///     <see cref="RegistrationTests" />, which only checks registration presence by name, these tests invoke the
///     <see cref="HealthCheckRegistration.Factory" /> produced by the Spark and observe the real behavior of the
///     resulting <see cref="HttpGetHealthCheck" />: which URL it probes (must be <c>HealthUrl</c>, NOT
///     <c>IngestionEndpoint</c>), and that the registration's <c>FailureStatus</c>, <c>Tags</c> and <c>Timeout</c>
///     are the ones wired from settings.
/// </summary>
public sealed class HealthCheckFactoryTests
{
    private static HostApplicationBuilder CreateBuilder() =>
        Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [] });

    /// <summary>
    ///     A minimal loopback HTTP server. Records every requested path and answers each configured path with a
    ///     fixed status code (defaulting to 404 for anything unmapped). Lets a test prove which absolute URL the
    ///     Spark-built health check actually hit.
    /// </summary>
    private sealed class LoopbackServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly Dictionary<string, HttpStatusCode> _routes;
        private readonly List<string> _requestedPaths = [];
        private readonly Lock _sync = new();

        public LoopbackServer(Dictionary<string, HttpStatusCode> routes)
        {
            _routes = routes;
            var port = GetFreePort();
            BaseUrl = $"http://127.0.0.1:{port}";
            _listener.Prefixes.Add($"{BaseUrl}/");
            _listener.Start();
            _ = Task.Run(Loop);
        }

        public string BaseUrl { get; }

        public IReadOnlyList<string> RequestedPaths
        {
            get
            {
                lock (_sync) return _requestedPaths.ToArray();
            }
        }

        private async Task Loop()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch
                {
                    return;
                }

                var path = context.Request.Url!.AbsolutePath;
                lock (_sync) _requestedPaths.Add(path);

                context.Response.StatusCode =
                    (int)(_routes.TryGetValue(path, out var status) ? status : HttpStatusCode.NotFound);
                context.Response.Close();
            }
        }

        private static int GetFreePort()
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        public void Dispose()
        {
            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch
            {
                // best-effort teardown
            }
        }
    }

    private static HealthCheckRegistration GetSeqRegistration(IServiceProvider sp)
    {
        var registrations = sp.GetRequiredService<
                Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations;
        return Assert.Single(registrations,
            r => r.Name.StartsWith(SeqOpenTelemetryExporterSpark.Name));
    }

    /// <summary>
    ///     The Spark-built health check must probe the configured <c>HealthUrl</c>. To pin the URI source, the
    ///     <c>HealthUrl</c> points at a loopback path that answers 200 while <c>IngestionEndpoint</c> points at a
    ///     DIFFERENT path that answers 500. A Healthy result and a recorded hit on the health path — with no hit on
    ///     the ingestion path — fails any mutant that swaps <c>HealthUrl</c> for <c>IngestionEndpoint</c> (or hardcodes
    ///     a wrong/empty URI).
    /// </summary>
    [Fact]
    public async Task RegisteredFactory_BuildsCheck_That_Probes_HealthUrl_Not_IngestionEndpoint()
    {
        using var server = new LoopbackServer(new Dictionary<string, HttpStatusCode>
        {
            ["/health"] = HttpStatusCode.OK,
            ["/ingest/otlp/v1/logs"] = HttpStatusCode.InternalServerError
        });

        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter(
            configureSettings: s => s.Enabled = true,
            configureOptions: o =>
            {
                // Distinct from HealthUrl: if the factory wrongly read IngestionEndpoint the probe would 500.
                o.IngestionEndpoint = $"{server.BaseUrl}/ingest";
                o.HealthUrl = $"{server.BaseUrl}/health";
            });

        using var host = builder.Build();
        var sp = host.Services;

        var registration = GetSeqRegistration(sp);
        var check = registration.Factory(sp);

        var context = new HealthCheckContext { Registration = registration };
        var result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        // Healthy proves the check hit the 200 /health route, i.e. it used HealthUrl.
        Assert.Equal(HealthStatus.Healthy, result.Status);

        // And prove it observably by the recorded path: /health was hit, the ingestion route was not.
        Assert.Contains("/health", server.RequestedPaths);
        Assert.DoesNotContain(server.RequestedPaths, p => p.StartsWith("/ingest"));
    }

    /// <summary>
    ///     Companion to the above: when <c>HealthUrl</c> points at a path that returns 503, the Spark-built check
    ///     must report the registration's configured <c>FailureStatus</c> (here <see cref="HealthStatus.Degraded" />),
    ///     not a hardcoded Unhealthy. This confirms both the URI source and that <c>FailureStatus</c> flows from
    ///     settings into the registration used by the factory-built check.
    /// </summary>
    [Fact]
    public async Task RegisteredFactory_Check_Reports_Configured_FailureStatus_On_HealthUrl_Failure()
    {
        using var server = new LoopbackServer(new Dictionary<string, HttpStatusCode>
        {
            ["/health"] = HttpStatusCode.ServiceUnavailable
        });

        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter(
            configureSettings: s =>
            {
                s.Enabled = true;
                s.HealthChecks.FailureStatus = HealthStatus.Degraded;
            },
            configureOptions: o =>
            {
                o.IngestionEndpoint = $"{server.BaseUrl}/ingest";
                o.HealthUrl = $"{server.BaseUrl}/health";
            });

        using var host = builder.Build();
        var sp = host.Services;

        var registration = GetSeqRegistration(sp);
        Assert.Equal(HealthStatus.Degraded, registration.FailureStatus);

        var check = registration.Factory(sp);
        var context = new HealthCheckContext { Registration = registration };
        var result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        // 503 on the health path -> the registration's FailureStatus, not a hardcoded Unhealthy.
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("/health", server.RequestedPaths);
    }

    /// <summary>
    ///     The registration built by the Spark must carry the Spark name tag plus the caller-configured tags, and the
    ///     settings-configured timeout. This guards the tag/timeout arguments of the
    ///     <see cref="HealthCheckRegistration" /> against mutation.
    /// </summary>
    [Fact]
    public void RegisteredCheck_Carries_SparkTag_CustomTags_And_ConfiguredTimeout()
    {
        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter(
            configureSettings: s =>
            {
                s.Enabled = true;
                s.HealthChecks.Tags = ["custom-tag"];
                s.HealthChecks.Timeout = TimeSpan.FromSeconds(7);
            },
            configureOptions: o =>
            {
                o.IngestionEndpoint = "http://seq:5341";
                o.HealthUrl = "http://seq:5341/health";
            });

        using var host = builder.Build();
        var registration = GetSeqRegistration(host.Services);

        Assert.Contains(SeqOpenTelemetryExporterSpark.Name, registration.Tags);
        Assert.Contains("custom-tag", registration.Tags);
        Assert.Equal(TimeSpan.FromSeconds(7), registration.Timeout);
    }

    /// <summary>
    ///     A HealthUrl pointing at a port with nothing listening must resolve to the registered FailureStatus with the
    ///     transport exception attached — exercising the Spark-built check against a real dead socket rather than a
    ///     hand-rolled <see cref="HttpGetHealthCheck" /> (which is what the old
    ///     <c>HealthCheck_UnreachableUrl_ReportsUnhealthy</c> did).
    /// </summary>
    [Fact]
    public async Task RegisteredFactory_Check_Reports_Unhealthy_On_Dead_HealthUrl()
    {
        var deadPort = GetFreePort();

        var builder = CreateBuilder();
        builder.IgniteSeqOpenTelemetryExporter(
            configureSettings: s => s.Enabled = true,
            configureOptions: o =>
            {
                o.IngestionEndpoint = "http://seq:5341";
                o.HealthUrl = $"http://127.0.0.1:{deadPort}/health";
            });

        using var host = builder.Build();
        var sp = host.Services;

        var registration = GetSeqRegistration(sp);
        var check = registration.Factory(sp);
        var context = new HealthCheckContext { Registration = registration };

        var result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
