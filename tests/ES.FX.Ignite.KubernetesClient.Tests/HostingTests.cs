using ES.FX.Ignite.KubernetesClient;
using ES.FX.Ignite.KubernetesClient.Configuration;
using ES.FX.Ignite.KubernetesClient.Hosting;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Exceptions;
using k8s;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.KubernetesClient.Tests;

public class HostingTests
{
    /// <summary>
    ///     Builds an in-memory host builder. A cluster is never contacted; we supply a fake
    ///     <see cref="KubernetesClientConfiguration" /> so <see cref="IKubernetes" /> construction never
    ///     touches a real kubeconfig or the network.
    /// </summary>
    private static HostApplicationBuilder CreateBuilder() => Host.CreateEmptyApplicationBuilder(null);

    private static KubernetesClientConfiguration FakeConfig() =>
        new() { Host = "https://kubernetes.invalid:6443" };

    [Fact]
    public void Ignite_RegistersSettings_IKubernetes_AndConfiguration()
    {
        var builder = CreateBuilder();

        builder.IgniteKubernetesClient(kubernetesClientConfigurationFactory: _ => FakeConfig());

        var services = builder.Build().Services;

        Assert.NotNull(services.GetRequiredService<KubernetesClientSparkSettings>());
        Assert.NotNull(services.GetRequiredService<KubernetesClientConfiguration>());
        var client = services.GetRequiredService<IKubernetes>();
        Assert.NotNull(client);
        Assert.IsAssignableFrom<Kubernetes>(client);
    }

    [Fact]
    public void Ignite_DoesNotAllowReconfiguration_ForSameServiceKey()
    {
        var builder = CreateBuilder();

        builder.IgniteKubernetesClient(kubernetesClientConfigurationFactory: _ => FakeConfig());

        Assert.Throws<ReconfigurationNotSupportedException>(() =>
            builder.IgniteKubernetesClient(kubernetesClientConfigurationFactory: _ => FakeConfig()));
    }

    [Fact]
    public void Ignite_AllowsDifferentServiceKeys()
    {
        var builder = CreateBuilder();

        builder.IgniteKubernetesClient(serviceKey: "a", kubernetesClientConfigurationFactory: _ => FakeConfig());
        // Different key => different guard bucket => must not throw.
        builder.IgniteKubernetesClient(serviceKey: "b", kubernetesClientConfigurationFactory: _ => FakeConfig());

        var services = builder.Build().Services;

        Assert.NotNull(services.GetRequiredKeyedService<IKubernetes>("a"));
        Assert.NotNull(services.GetRequiredKeyedService<IKubernetes>("b"));
    }

    [Fact]
    public void Ignite_KeyedRegistration_IsNotResolvableAsDefault()
    {
        var builder = CreateBuilder();

        builder.IgniteKubernetesClient(serviceKey: "keyed", kubernetesClientConfigurationFactory: _ => FakeConfig());

        var services = builder.Build().Services;

        Assert.NotNull(services.GetRequiredKeyedService<IKubernetes>("keyed"));
        // A keyed registration must NOT satisfy an unkeyed resolve.
        Assert.Null(services.GetService<IKubernetes>());
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void Ignite_HonorsRequestedLifetime(ServiceLifetime lifetime)
    {
        var builder = CreateBuilder();

        builder.IgniteKubernetesClient(
            kubernetesClientConfigurationFactory: _ => FakeConfig(),
            lifetime: lifetime);

        var descriptor = Assert.Single(builder.Services, d => d.ServiceType == typeof(IKubernetes));
        Assert.Equal(lifetime, descriptor.Lifetime);

        var configDescriptor =
            Assert.Single(builder.Services, d => d.ServiceType == typeof(KubernetesClientConfiguration));
        Assert.Equal(lifetime, configDescriptor.Lifetime);
    }

    [Fact]
    public void Singleton_ReturnsSameInstance()
    {
        var builder = CreateBuilder();
        builder.IgniteKubernetesClient(
            kubernetesClientConfigurationFactory: _ => FakeConfig(),
            lifetime: ServiceLifetime.Singleton);

        var services = builder.Build().Services;

        var first = services.GetRequiredService<IKubernetes>();
        var second = services.GetRequiredService<IKubernetes>();
        Assert.Same(first, second);
    }

    [Fact]
    public void Transient_ReturnsFreshInstances()
    {
        var builder = CreateBuilder();
        builder.IgniteKubernetesClient(
            kubernetesClientConfigurationFactory: _ => FakeConfig(),
            lifetime: ServiceLifetime.Transient);

        var services = builder.Build().Services;

        var first = services.GetRequiredService<IKubernetes>();
        var second = services.GetRequiredService<IKubernetes>();
        Assert.NotSame(first, second);
    }

    [Fact]
    public void ConfigurationFactory_IsInvoked_PerClientConstruction()
    {
        var builder = CreateBuilder();
        var factoryCalls = 0;

        builder.IgniteKubernetesClient(
            kubernetesClientConfigurationFactory: _ =>
            {
                factoryCalls++;
                return FakeConfig();
            },
            lifetime: ServiceLifetime.Transient);

        var services = builder.Build().Services;

        Assert.Equal(0, factoryCalls);
        _ = services.GetRequiredService<IKubernetes>();
        _ = services.GetRequiredService<IKubernetes>();
        // Transient config + transient client => the factory runs once per resolved client.
        Assert.Equal(2, factoryCalls);
    }

    [Fact]
    public void ConfigureConfiguration_Delegate_ReceivesFactoryConfiguration()
    {
        var builder = CreateBuilder();
        KubernetesClientConfiguration? seen = null;

        builder.IgniteKubernetesClient(
            kubernetesClientConfigurationFactory: _ => FakeConfig(),
            configureKubernetesClientConfiguration: (_, config) => seen = config);

        var services = builder.Build().Services;
        var resolved = services.GetRequiredService<KubernetesClientConfiguration>();

        Assert.NotNull(seen);
        Assert.Same(resolved, seen);
        Assert.StartsWith("https://kubernetes.invalid:6443", resolved.Host);
    }

    [Fact]
    public void SkipTlsVerify_NotSet_LeavesConfigurationUntouched()
    {
        var builder = CreateBuilder();

        // Factory config has SkipTlsVerify default (false). Options.SkipTlsVerify is null,
        // so the spark must NOT overwrite it.
        var factoryConfig = FakeConfig();
        factoryConfig.SkipTlsVerify = true; // pre-existing value that must survive

        builder.IgniteKubernetesClient(kubernetesClientConfigurationFactory: _ => factoryConfig);

        var config = builder.Build().Services.GetRequiredService<KubernetesClientConfiguration>();

        // Untouched: the value the factory produced (true) is preserved.
        Assert.True(config.SkipTlsVerify);
    }

    [Fact]
    public void SkipTlsVerify_SetTrue_ViaOptions_IsApplied()
    {
        var builder = CreateBuilder();

        var factoryConfig = FakeConfig();
        factoryConfig.SkipTlsVerify = false;

        builder.IgniteKubernetesClient(
            configureOptions: options => options.SkipTlsVerify = true,
            kubernetesClientConfigurationFactory: _ => factoryConfig);

        var config = builder.Build().Services.GetRequiredService<KubernetesClientConfiguration>();

        Assert.True(config.SkipTlsVerify);
    }

    [Fact]
    public void SkipTlsVerify_SetFalse_ViaOptions_OverridesFactoryTrue()
    {
        var builder = CreateBuilder();

        var factoryConfig = FakeConfig();
        factoryConfig.SkipTlsVerify = true;

        builder.IgniteKubernetesClient(
            configureOptions: options => options.SkipTlsVerify = false,
            kubernetesClientConfigurationFactory: _ => factoryConfig);

        var config = builder.Build().Services.GetRequiredService<KubernetesClientConfiguration>();

        // Options explicitly set to false => must be applied, overriding the factory's true.
        Assert.False(config.SkipTlsVerify);
    }

    [Fact]
    public void SkipTlsVerify_BoundFromConfiguration_IsApplied()
    {
        var builder = CreateBuilder();

        // Options bind directly to the section path (no "Settings" sub-section).
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{KubernetesClientSpark.ConfigurationSectionPath}:{nameof(KubernetesClientSparkOptions.SkipTlsVerify)}",
                true.ToString())
        ]);

        var factoryConfig = FakeConfig();
        factoryConfig.SkipTlsVerify = false;

        builder.IgniteKubernetesClient(kubernetesClientConfigurationFactory: _ => factoryConfig);

        var config = builder.Build().Services.GetRequiredService<KubernetesClientConfiguration>();
        Assert.True(config.SkipTlsVerify);
    }

    [Fact]
    public void DelegatingHandlers_Factory_IsInvoked_WithResolvingServiceProvider()
    {
        var builder = CreateBuilder();

        var sentinel = new object();
        builder.Services.AddSingleton(sentinel);

        var invocations = 0;
        IServiceProvider? capturedProvider = null;

        builder.IgniteKubernetesClient(
            kubernetesClientConfigurationFactory: _ => FakeConfig(),
            kubernetesClientDelegatingHandlers: sp =>
            {
                invocations++;
                capturedProvider = sp;
                return [new TrackingHandler()];
            });

        var services = builder.Build().Services;

        Assert.Equal(0, invocations);
        _ = services.GetRequiredService<IKubernetes>();

        Assert.Equal(1, invocations);
        Assert.NotNull(capturedProvider);
        // The provider passed to the handler factory is the resolving provider and can resolve services.
        Assert.Same(sentinel, capturedProvider!.GetRequiredService<object>());
    }

    [Fact]
    public void DelegatingHandlers_Factory_ProducesFreshHandlers_PerClient()
    {
        var builder = CreateBuilder();

        var produced = new List<TrackingHandler>();

        builder.IgniteKubernetesClient(
            kubernetesClientConfigurationFactory: _ => FakeConfig(),
            kubernetesClientDelegatingHandlers: _ =>
            {
                var handler = new TrackingHandler();
                produced.Add(handler);
                return [handler];
            },
            lifetime: ServiceLifetime.Transient);

        var services = builder.Build().Services;

        _ = services.GetRequiredService<IKubernetes>();
        _ = services.GetRequiredService<IKubernetes>();

        // A fresh handler set is produced for every client construction (required for
        // non-singleton lifetimes since a DelegatingHandler cannot be re-parented).
        Assert.Equal(2, produced.Count);
        Assert.NotSame(produced[0], produced[1]);
    }

    [Fact]
    public void NoDelegatingHandlers_StillConstructsClient()
    {
        var builder = CreateBuilder();

        builder.IgniteKubernetesClient(kubernetesClientConfigurationFactory: _ => FakeConfig());

        var client = builder.Build().Services.GetRequiredService<IKubernetes>();
        Assert.NotNull(client);
    }

    [Fact]
    public void ConfigureSettings_Delegate_SeesBoundValues_AndCanMutate()
    {
        var builder = CreateBuilder();

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{KubernetesClientSpark.ConfigurationSectionPath}:{SparkConfig.Settings}:{nameof(KubernetesClientSparkSettings.HealthChecks)}:{nameof(HealthCheckSettings.Enabled)}",
                false.ToString())
        ]);

        var sawEnabledFromConfig = false;

        builder.IgniteKubernetesClient(
            configureSettings: settings =>
            {
                sawEnabledFromConfig = settings.HealthChecks.Enabled;
                settings.HealthChecks.Enabled = true;
            },
            kubernetesClientConfigurationFactory: _ => FakeConfig());

        var resolved = builder.Build().Services.GetRequiredService<KubernetesClientSparkSettings>();

        // configureSettings ran after configuration binding (which set Enabled=false)...
        Assert.False(sawEnabledFromConfig);
        // ...and its mutation is reflected on the resolved settings.
        Assert.True(resolved.HealthChecks.Enabled);
    }

    [Fact]
    public void Settings_RegisteredAsKeyed_MatchingServiceKey()
    {
        var builder = CreateBuilder();

        builder.IgniteKubernetesClient(serviceKey: "primary",
            kubernetesClientConfigurationFactory: _ => FakeConfig());

        var services = builder.Build().Services;

        Assert.NotNull(services.GetRequiredKeyedService<KubernetesClientSparkSettings>("primary"));
    }

    private sealed class TrackingHandler : DelegatingHandler
    {
    }
}
