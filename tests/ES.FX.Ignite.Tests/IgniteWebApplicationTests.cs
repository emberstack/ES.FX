using System.Net;
using ES.FX.Ignite.Hosting;
using ES.FX.Ignite.Spark.HealthChecks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Tests;

/// <summary>
///     Covers the post-build (phase 2) <see cref="IgniteHostingExtensions.Ignite(IHost)" /> activation for
///     <see cref="WebApplication" /> hosts: the mapped readiness/liveness health-check endpoints, the
///     liveness "live"-tag filtering, custom endpoint paths, the disabled case, and the argument guard.
///     Each test drives an in-process <see cref="TestServer" /> so the actual HTTP behavior is asserted.
/// </summary>
public class IgniteWebApplicationTests
{
    private static WebApplicationBuilder CreateWebBuilder(
        IEnumerable<KeyValuePair<string, string?>>? configuration = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = "IgniteTestApp",
            EnvironmentName = Environments.Production
        });
        builder.WebHost.UseTestServer();
        if (configuration is not null)
            builder.Configuration.AddInMemoryCollection(configuration);
        return builder;
    }

    private static HttpClient CreateClient(WebApplication app) =>
        app.GetTestServer().CreateClient();

    [Fact]
    public void Ignite_Host_NullHost_Throws()
    {
        IHost host = null!;
        Assert.Throws<ArgumentNullException>(() => host.Ignite());
    }

    [Fact]
    public async Task Ignite_NonWebHost_IsNoOp_And_ReturnsSameHost()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Ignite();
        using var host = builder.Build();

        // For non-WebApplication hosts the second phase is a no-op that returns the host unchanged.
        var result = host.Ignite();
        Assert.Same(host, result);

        await Task.CompletedTask;
    }

    [Fact]
    public async Task Ignite_MapsReadinessAndLivenessEndpoints()
    {
        var builder = CreateWebBuilder();
        builder.Ignite();
        await using var app = builder.Build();
        app.Ignite();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = CreateClient(app);

        var ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        var live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task Ignite_CustomHealthCheckPaths_AreHonored()
    {
        var builder = CreateWebBuilder(new Dictionary<string, string?>
        {
            ["Ignite:Settings:AspNetCore:HealthChecks:ReadinessEndpointPath"] = "/custom/ready",
            ["Ignite:Settings:AspNetCore:HealthChecks:LivenessEndpointPath"] = "/custom/live"
        });
        builder.Ignite();
        await using var app = builder.Build();
        app.Ignite();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = CreateClient(app);

        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/custom/ready", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/custom/live", TestContext.Current.CancellationToken)).StatusCode);
        // Default paths must NOT be mapped when custom ones are configured.
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/health/ready", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Ignite_HealthChecksDisabled_EndpointsNotMapped()
    {
        var builder = CreateWebBuilder(new Dictionary<string, string?>
        {
            ["Ignite:Settings:AspNetCore:HealthChecks:Enabled"] = "false"
        });
        builder.Ignite();
        await using var app = builder.Build();
        app.Ignite();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = CreateClient(app);

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/health/ready", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/health/live", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Ignite_Liveness_OnlyRunsLiveTaggedChecks_ReadinessRunsAll()
    {
        // A non-live-tagged UNHEALTHY check must fail readiness but be excluded from liveness.
        var builder = CreateWebBuilder();
        builder.Ignite();
        builder.Services.AddHealthChecks()
            .AddCheck("readiness-only", () => HealthCheckResult.Unhealthy("down"), ["ready"]);

        await using var app = builder.Build();
        app.Ignite();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = CreateClient(app);

        // Readiness runs the unhealthy check -> 503.
        var ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);

        // Liveness only considers "live"-tagged checks; the unhealthy one is filtered out -> 200.
        var live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task Ignite_Liveness_RunsLiveTaggedUnhealthyCheck()
    {
        var builder = CreateWebBuilder();
        builder.Ignite();
        builder.Services.AddHealthChecks()
            .AddCheck("live-check", () => HealthCheckResult.Unhealthy("dead"),
                [HealthChecksTags.Live]);

        await using var app = builder.Build();
        app.Ignite();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = CreateClient(app);

        // A "live"-tagged unhealthy check fails liveness.
        var live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, live.StatusCode);
    }

    [Fact]
    public async Task Ignite_CustomResponseWriter_IsUsedForHealthEndpoints()
    {
        const string marker = "IGNITE-CUSTOM-WRITER";
        var builder = CreateWebBuilder();
        builder.Ignite(settings =>
            settings.AspNetCore.HealthChecks.ResponseWriter = async (context, _) =>
            {
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync(marker, context.RequestAborted);
            });

        await using var app = builder.Build();
        app.Ignite();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = CreateClient(app);

        var ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        var body = await ready.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(marker, body);
    }

    [Fact]
    public async Task Ignite_TraceIdResponseHeaderMiddleware_IsWiredByDefault()
    {
        var builder = CreateWebBuilder();
        builder.Ignite(); // UseTraceIdResponseHeader defaults to true
        await using var app = builder.Build();
        app.Ignite();
        app.MapGet("/ping", () => "pong");
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = CreateClient(app);
        var response = await client.GetAsync("/ping", TestContext.Current.CancellationToken);

        // TraceIdResponseHeaderMiddleware emits the X-Trace-Id response header.
        Assert.True(response.Headers.Contains("X-Trace-Id"),
            "Expected the 'X-Trace-Id' response header emitted by TraceIdResponseHeaderMiddleware.");
    }

    [Fact]
    public async Task Ignite_TraceIdResponseHeaderMiddlewareDisabled_HeaderAbsent()
    {
        var builder = CreateWebBuilder(new Dictionary<string, string?>
        {
            ["Ignite:Settings:AspNetCore:UseTraceIdResponseHeader"] = "false"
        });
        builder.Ignite();
        await using var app = builder.Build();
        app.Ignite();
        app.MapGet("/ping", () => "pong");
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = CreateClient(app);
        var response = await client.GetAsync("/ping", TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("X-Trace-Id"));
    }

    [Fact]
    public async Task Ignite_WebApplication_ReturnsSameHost()
    {
        var builder = CreateWebBuilder();
        builder.Ignite();
        await using var app = builder.Build();

        var result = app.Ignite();
        Assert.Same(app, result);

        await Task.CompletedTask;
    }
}