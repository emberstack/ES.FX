using System.Diagnostics;
using Azure.Data.Tables;
using ES.FX.Ignite.Azure.Common.Hosting;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Configuration.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;

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
            null,
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
            key,
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
            null,
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
            null,
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
            key,
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
    public void HealthCheck_Factory_ResolvesDefaultClient_WhenServiceKeyIsNull()
    {
        var services = new ServiceCollection();

        // With a null service key, the health-check registration factory resolves the client via
        // GetRequiredKeyedService<TClient>(null), which maps to the DEFAULT (unkeyed) registration.
        // Register a default client so the factory can actually build.
        services.IgniteAzureClient<TableServiceClient, TableClientOptions>(
            null,
            FakeClientSection());

        TableServiceClient? capturedClient = null;
        services.IgniteAzureClientObservability<TableServiceClient>(
            null,
            new TracingSettings { Enabled = false },
            new HealthCheckSettings { Enabled = true },
            (_, client) =>
            {
                capturedClient = client;
                return new StubHealthCheck();
            });

        using var provider = services.BuildServiceProvider();
        var registration = Assert.Single(GetRegistrations(provider));

        // Forcing the null-key factory to run must resolve the default client and yield a health check.
        var healthCheck = registration.Factory(provider);
        Assert.NotNull(healthCheck);
        Assert.NotNull(capturedClient);
        Assert.Same(provider.GetRequiredService<TableServiceClient>(), capturedClient);
    }

    [Fact]
    public void
        HealthCheck_WhitespaceServiceKey_NormalizedToDefault_NameHasNoKeySuffix_AndFactoryResolvesDefaultClient()
    {
        var services = new ServiceCollection();

        // A default client for the null-key (normalized) factory lookup to resolve.
        services.IgniteAzureClient<TableServiceClient, TableClientOptions>(
            null,
            FakeClientSection());

        TableServiceClient? capturedClient = null;
        // Whitespace key must be normalized to null: name shape "Azure-{ClientTypeName}" (no "-[   ]"),
        // and the factory must resolve the DEFAULT client (not a whitespace-keyed one).
        services.IgniteAzureClientObservability<TableServiceClient>(
            "   ",
            new TracingSettings { Enabled = false },
            new HealthCheckSettings { Enabled = true },
            (_, client) =>
            {
                capturedClient = client;
                return new StubHealthCheck();
            });

        using var provider = services.BuildServiceProvider();
        var registration = Assert.Single(GetRegistrations(provider));

        // Normalized name: no whitespace-key suffix.
        Assert.Equal($"Azure-{nameof(TableServiceClient)}", registration.Name);
        Assert.DoesNotContain("[", registration.Name);

        // The normalized (null-key) factory resolves the default client.
        var healthCheck = registration.Factory(provider);
        Assert.NotNull(healthCheck);
        Assert.Same(provider.GetRequiredService<TableServiceClient>(), capturedClient);
    }

    [Fact]
    public void Tracing_Enabled_RegistersOpenTelemetryServices()
    {
        var services = new ServiceCollection();

        services.IgniteAzureClientObservability<TableServiceClient>(
            null,
            new TracingSettings { Enabled = true },
            new HealthCheckSettings { Enabled = false },
            StubHealthCheckFactory);

        // AddOpenTelemetry().WithTracing(...) registers hosted/configuration services.
        // Presence of any OpenTelemetry-registered descriptor proves tracing wiring ran.
        Assert.Contains(services, d =>
            d.ServiceType.FullName?.Contains("OpenTelemetry", StringComparison.Ordinal) == true);
    }

    /// <summary>
    ///     Behavioral proof that tracing subscribes to the Azure client's namespaced trace source
    ///     pattern <c>{typeof(TClient).Namespace}.*</c>. Builds the real <see cref="TracerProvider" /> and
    ///     verifies that an <see cref="ActivitySource" /> whose name lives UNDER the client namespace
    ///     (<c>Azure.Data.Tables.*</c>) is actually listened to (its activities are sampled/recorded),
    ///     while a source outside that namespace is not.
    ///     This kills the surviving mutations on
    ///     <c>traceBuilder.AddSource($"{typeof(TClient).Namespace}.*")</c>: dropping the <c>.*</c> suffix,
    ///     swapping <c>Namespace</c> for <c>Name</c>, or hardcoding a wrong literal source name all leave
    ///     the child source unsubscribed and fail this test.
    /// </summary>
    [Fact]
    public void Tracing_Enabled_SubscribesToClientNamespaceSourcePattern()
    {
        var clientNamespace = typeof(TableServiceClient).Namespace;
        Assert.Equal("Azure.Data.Tables", clientNamespace);

        var services = new ServiceCollection();

        services.IgniteAzureClientObservability<TableServiceClient>(
            null,
            new TracingSettings { Enabled = true },
            new HealthCheckSettings { Enabled = false },
            StubHealthCheckFactory);

        using var provider = services.BuildServiceProvider();

        // Resolving the TracerProvider forces the deferred WithTracing/AddSource callback to run and
        // installs the ActivityListener for the subscribed source name pattern.
        var tracerProvider = provider.GetRequiredService<TracerProvider>();
        Assert.NotNull(tracerProvider);

        // A source whose name is a child of the client namespace => matched by "Azure.Data.Tables.*".
        using var inNamespaceSource = new ActivitySource($"{clientNamespace}.Probe.{Guid.NewGuid():N}");
        // A source outside the client namespace => must NOT be matched.
        using var outsideNamespaceSource = new ActivitySource($"Unrelated.Probe.{Guid.NewGuid():N}");

        using var inNamespaceActivity = inNamespaceSource.StartActivity("in-namespace");
        using var outsideNamespaceActivity = outsideNamespaceSource.StartActivity("outside-namespace");

        // The namespaced source is subscribed: an activity is created and flagged for recording.
        Assert.NotNull(inNamespaceActivity);
        Assert.True(inNamespaceActivity.IsAllDataRequested,
            "Activity from the client-namespace source must be sampled/recorded by the subscribed listener.");

        // A source outside the client namespace has no listener => no activity is created.
        Assert.Null(outsideNamespaceActivity);
    }

    /// <summary>
    ///     Guards the exact <c>.*</c> wildcard boundary: the trace source name is
    ///     <c>{Namespace}.*</c>, NOT the bare namespace and NOT a broader wildcard. An
    ///     <see cref="ActivitySource" /> named EXACTLY the client namespace (with no trailing segment)
    ///     is not a child of the pattern and must remain unsubscribed. This kills a mutation that drops
    ///     the <c>.</c> before <c>*</c> (turning it into an overly-broad prefix match) or replaces the
    ///     pattern with the bare namespace.
    /// </summary>
    [Fact]
    public void Tracing_Enabled_DoesNotSubscribeToBareNamespaceSource()
    {
        var clientNamespace = typeof(TableServiceClient).Namespace;

        var services = new ServiceCollection();

        services.IgniteAzureClientObservability<TableServiceClient>(
            null,
            new TracingSettings { Enabled = true },
            new HealthCheckSettings { Enabled = false },
            StubHealthCheckFactory);

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<TracerProvider>();

        // Source named exactly the namespace (e.g. "Azure.Data.Tables") — the "{ns}.*" pattern only
        // matches names that have the namespace followed by a dot, so this one is NOT subscribed.
        using var bareNamespaceSource = new ActivitySource(clientNamespace!);
        using var bareNamespaceActivity = bareNamespaceSource.StartActivity("bare");

        Assert.Null(bareNamespaceActivity);
    }

    [Fact]
    public void Tracing_Disabled_DoesNotRegisterOpenTelemetryServices()
    {
        var services = new ServiceCollection();

        services.IgniteAzureClientObservability<TableServiceClient>(
            null,
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