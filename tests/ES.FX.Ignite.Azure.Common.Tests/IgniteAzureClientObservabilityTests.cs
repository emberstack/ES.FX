using Azure.Data.Tables;
using ES.FX.Ignite.Azure.Common.Hosting;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Configuration.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ES.FX.Ignite.Azure.Common.Tests;

/// <summary>
///     Functional coverage of
///     <see cref="AzureCommonHostingExtensions.IgniteAzureClientObservability{TClient}" />.
///     Asserts the tracing + health-check registrations produced from
///     <see cref="TracingSettings" /> / <see cref="HealthCheckSettings" />. No live Azure is contacted;
///     the health check factory is a stub and is never actually executed.
/// </summary>
public class IgniteAzureClientObservabilityTests
{
    private const string FakeConnectionString =
        "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;" +
        "AccountKey=AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyA=;" +
        "TableEndpoint=https://devstoreaccount1.table.core.windows.net/;";

    private static IConfigurationSection FakeClientSection(string sectionName = "client")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{sectionName}:connectionString"] = FakeConnectionString
            })
            .Build();
        return configuration.GetSection(sectionName);
    }

    private static IHealthCheck StubHealthCheckFactory(IServiceProvider _, TableServiceClient __) =>
        new StubHealthCheck();

    private static IReadOnlyList<HealthCheckRegistration> GetRegistrations(ServiceProvider provider) =>
        provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.ToList();

    [Fact]
    public void HealthCheck_Enabled_RegistersRegistration_WithExpectedNameAndTags()
    {
        var services = new ServiceCollection();

        var healthCheckSettings = new HealthCheckSettings
        {
            Enabled = true,
            Tags = ["custom-tag"]
        };

        services.IgniteAzureClientObservability<TableServiceClient>(
            serviceKey: null,
            new TracingSettings { Enabled = false },
            healthCheckSettings,
            StubHealthCheckFactory);

        using var provider = services.BuildServiceProvider();
        var registrations = GetRegistrations(provider);

        var registration = Assert.Single(registrations);

        // Name shape: "Azure-{ClientTypeName}" when no service key.
        Assert.Equal($"Azure-{nameof(TableServiceClient)}", registration.Name);

        // Tags: ["Azure", "{ClientTypeName}", ...settings.Tags]
        Assert.Contains("Azure", registration.Tags);
        Assert.Contains(nameof(TableServiceClient), registration.Tags);
        Assert.Contains("custom-tag", registration.Tags);
    }

    [Fact]
    public void HealthCheck_Enabled_WithServiceKey_IncludesKeyInName()
    {
        const string key = "primary";
        var services = new ServiceCollection();

        services.IgniteAzureClientObservability<TableServiceClient>(
            serviceKey: key,
            new TracingSettings { Enabled = false },
            new HealthCheckSettings { Enabled = true },
            StubHealthCheckFactory);

        using var provider = services.BuildServiceProvider();
        var registration = Assert.Single(GetRegistrations(provider));

        // Name shape: "Azure-{ClientTypeName}-[{serviceKey}]" when a service key is present.
        Assert.Equal($"Azure-{nameof(TableServiceClient)}-[{key}]", registration.Name);
    }

    [Fact]
    public void HealthCheck_Enabled_HonorsFailureStatusAndTimeout()
    {
        var services = new ServiceCollection();

        var timeout = TimeSpan.FromSeconds(7);
        var settings = new HealthCheckSettings
        {
            Enabled = true,
            FailureStatus = HealthStatus.Degraded,
            Timeout = timeout
        };

        services.IgniteAzureClientObservability<TableServiceClient>(
            serviceKey: null,
            new TracingSettings { Enabled = false },
            settings,
            StubHealthCheckFactory);

        using var provider = services.BuildServiceProvider();
        var registration = Assert.Single(GetRegistrations(provider));

        Assert.Equal(HealthStatus.Degraded, registration.FailureStatus);
        Assert.Equal(timeout, registration.Timeout);
    }

    [Fact]
    public void HealthCheck_Disabled_RegistersNoHealthCheck()
    {
        var services = new ServiceCollection();

        services.IgniteAzureClientObservability<TableServiceClient>(
            serviceKey: null,
            new TracingSettings { Enabled = false },
            new HealthCheckSettings { Enabled = false },
            StubHealthCheckFactory);

        using var provider = services.BuildServiceProvider();

        // No health checks were added at all.
        var registrationsOptions = provider.GetService<IOptions<HealthCheckServiceOptions>>();
        var registrations = registrationsOptions?.Value.Registrations ?? [];
        Assert.Empty(registrations);
    }

    [Fact]
    public void HealthCheck_Factory_ResolvesKeyedClient_WhenInstantiated()
    {
        const string key = "primary";
        var services = new ServiceCollection();

        // The health check factory resolves GetRequiredKeyedService<TClient>(serviceKey) at
        // instantiation time, so the client must be registered under that key for the
        // registration to actually build. Register the client first.
        services.IgniteAzureClient<TableServiceClient, TableClientOptions>(key, FakeClientSection());

        TableServiceClient? capturedClient = null;
        services.IgniteAzureClientObservability<TableServiceClient>(
            serviceKey: key,
            new TracingSettings { Enabled = false },
            new HealthCheckSettings { Enabled = true },
            (_, client) =>
            {
                capturedClient = client;
                return new StubHealthCheck();
            });

        using var provider = services.BuildServiceProvider();
        var registration = Assert.Single(GetRegistrations(provider));

        // Forcing the factory to run must yield a non-null health check and the keyed client.
        var healthCheck = registration.Factory(provider);
        Assert.NotNull(healthCheck);
        Assert.NotNull(capturedClient);
        Assert.Same(provider.GetRequiredKeyedService<TableServiceClient>(key), capturedClient);
    }

    [Fact]
    public void Tracing_Enabled_RegistersOpenTelemetryServices()
    {
        var services = new ServiceCollection();

        services.IgniteAzureClientObservability<TableServiceClient>(
            serviceKey: null,
            new TracingSettings { Enabled = true },
            new HealthCheckSettings { Enabled = false },
            StubHealthCheckFactory);

        // AddOpenTelemetry().WithTracing(...) registers hosted/configuration services.
        // Presence of any OpenTelemetry-registered descriptor proves tracing wiring ran.
        Assert.Contains(services, d =>
            d.ServiceType.FullName?.Contains("OpenTelemetry", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Tracing_Disabled_DoesNotRegisterOpenTelemetryServices()
    {
        var services = new ServiceCollection();

        services.IgniteAzureClientObservability<TableServiceClient>(
            serviceKey: null,
            new TracingSettings { Enabled = false },
            new HealthCheckSettings { Enabled = false },
            StubHealthCheckFactory);

        Assert.DoesNotContain(services, d =>
            d.ServiceType.FullName?.Contains("OpenTelemetry", StringComparison.Ordinal) == true);
    }

    private sealed class StubHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }
}
