using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.StackExchange.Redis.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using OpenTelemetry.Instrumentation.StackExchangeRedis;
using StackExchange.Redis;

namespace ES.FX.Ignite.StackExchange.Redis.Tests;

/// <summary>
///     Covers the observability wiring in <see cref="RedisHostingExtensions" />: health-check
///     registration name/tags/failure-status/timeout, keyed vs default multiplexer resolution in the
///     registration factory, and the tracing "configure once" guard.
/// </summary>
public class ObservabilityRegistrationTests
{
    private static ICollection<HealthCheckRegistration> GetRegistrations(IHost app) =>
        app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

    [Fact]
    public void HealthCheck_Registration_Default_Name_Tags_FailureStatus_Timeout()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteRedisClient(configureSettings: settings =>
        {
            settings.HealthChecks.Tags = ["custom-tag"];
            settings.HealthChecks.FailureStatus = HealthStatus.Degraded;
            settings.HealthChecks.Timeout = TimeSpan.FromSeconds(7);
        });

        var app = builder.Build();
        var registration = Assert.Single(GetRegistrations(app));

        Assert.Equal("Redis", registration.Name);
        Assert.Contains(nameof(Redis), registration.Tags);
        Assert.Contains("custom-tag", registration.Tags);
        Assert.Equal(HealthStatus.Degraded, registration.FailureStatus);
        Assert.Equal(TimeSpan.FromSeconds(7), registration.Timeout);
    }

    [Fact]
    public void HealthCheck_Registration_Keyed_Name_Is_Suffixed()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteRedisClient(serviceKey: "cache");

        var app = builder.Build();
        var registration = Assert.Single(GetRegistrations(app));

        Assert.Equal("Redis[cache]", registration.Name);
    }

    [Fact]
    public void HealthCheck_Factory_Resolves_Default_Multiplexer()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteRedisClient();

        // Replace the real (connecting) multiplexer registration with a mock so the factory can be
        // invoked without a live Redis server.
        var multiplexer = Mock.Of<IConnectionMultiplexer>();
        builder.Services.AddSingleton(multiplexer);

        var app = builder.Build();
        var registration = Assert.Single(GetRegistrations(app));

        // Invoking the factory proves it resolves IConnectionMultiplexer as a default (non-keyed)
        // service; a wrong GetRequiredKeyedService call would throw here.
        var healthCheck = registration.Factory(app.Services);
        Assert.NotNull(healthCheck);
    }

    [Fact]
    public void HealthCheck_Factory_Resolves_Keyed_Multiplexer()
    {
        const string key = "cache";
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteRedisClient(serviceKey: key);

        var multiplexer = Mock.Of<IConnectionMultiplexer>();
        builder.Services.AddKeyedSingleton(key, multiplexer);

        var app = builder.Build();
        var registration = Assert.Single(GetRegistrations(app));

        // Factory must resolve the keyed multiplexer; a default GetRequiredService would throw.
        var healthCheck = registration.Factory(app.Services);
        Assert.NotNull(healthCheck);
    }

    [Fact]
    public void Tracing_Configure_Once_Guard_Set_And_Enrich_Disabled()
    {
        const string guardKey = "Redis.Global.Tracing.Configure";

        var builder = Host.CreateEmptyApplicationBuilder(null);
        Assert.False(builder.IsGuardConfigurationKeySet(guardKey));

        // Two separate keyed clients, both with tracing enabled.
        builder.IgniteRedisClient(serviceKey: "a", configureSettings: s => s.Tracing.Enabled = true);
        builder.IgniteRedisClient(serviceKey: "b", configureSettings: s => s.Tracing.Enabled = true);

        // Global tracing was configured exactly once (guard was flipped on the first call).
        Assert.True(builder.IsGuardConfigurationKeySet(guardKey));

        var app = builder.Build();

        // The instrumentation options must have timing-event enrichment disabled.
        var instrumentationOptions = app.Services
            .GetRequiredService<IOptions<StackExchangeRedisInstrumentationOptions>>().Value;
        Assert.False(instrumentationOptions.EnrichActivityWithTimingEvents);
    }

    [Fact]
    public void Tracing_Guard_Not_Set_When_Tracing_Disabled()
    {
        const string guardKey = "Redis.Global.Tracing.Configure";

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteRedisClient(configureSettings: s => s.Tracing.Enabled = false);

        Assert.False(builder.IsGuardConfigurationKeySet(guardKey));
    }

    [Fact]
    public void HealthChecks_Not_Registered_When_Disabled()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteRedisClient(configureSettings: s => s.HealthChecks.Enabled = false);

        var app = builder.Build();
        Assert.Empty(GetRegistrations(app));
    }
}