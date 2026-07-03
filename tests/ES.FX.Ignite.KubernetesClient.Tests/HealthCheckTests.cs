using ES.FX.Ignite.KubernetesClient;
using ES.FX.Ignite.KubernetesClient.Configuration;
using ES.FX.Ignite.KubernetesClient.Hosting;
using ES.FX.Ignite.Spark.Configuration;
using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ES.FX.Ignite.KubernetesClient.Tests;

public class HealthCheckTests
{
    private static KubernetesClientConfiguration FakeConfig() =>
        new() { Host = "https://kubernetes.invalid:6443" };

    private static IEnumerable<HealthCheckRegistration> GetRegistrations(IServiceProvider services) =>
        services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

    [Fact]
    public void HealthCheck_Registered_ByDefault()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.IgniteKubernetesClient(kubernetesClientConfigurationFactory: _ => FakeConfig());

        var registrations = GetRegistrations(builder.Build().Services).ToList();

        var registration = Assert.Single(registrations);
        Assert.Equal(KubernetesClientSpark.Name, registration.Name);
    }

    [Fact]
    public void HealthCheck_Registration_ResolvesToKubernetesHealthCheck()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.IgniteKubernetesClient(kubernetesClientConfigurationFactory: _ => FakeConfig());

        var services = builder.Build().Services;
        var registration = Assert.Single(GetRegistrations(services));

        // The registration factory resolves the IKubernetes client and builds the health check.
        var instance = registration.Factory(services);
        Assert.NotNull(instance);
        Assert.IsAssignableFrom<IHealthCheck>(instance);
    }

    [Fact]
    public void HealthCheck_NotRegistered_WhenDisabled()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.IgniteKubernetesClient(
            configureSettings: settings => settings.HealthChecks.Enabled = false,
            kubernetesClientConfigurationFactory: _ => FakeConfig());

        var registrations = GetRegistrations(builder.Build().Services).ToList();
        Assert.Empty(registrations);
    }

    [Fact]
    public void HealthCheck_Name_IncludesServiceKey_WhenKeyed()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.IgniteKubernetesClient(serviceKey: "cluster1",
            kubernetesClientConfigurationFactory: _ => FakeConfig());

        var registration = Assert.Single(GetRegistrations(builder.Build().Services));
        Assert.Equal($"{KubernetesClientSpark.Name}[cluster1]", registration.Name);
    }

    [Fact]
    public void HealthCheck_IncludesSparkNameTag_AndCustomTags()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.IgniteKubernetesClient(
            configureSettings: settings => settings.HealthChecks.Tags = ["custom-tag"],
            kubernetesClientConfigurationFactory: _ => FakeConfig());

        var registration = Assert.Single(GetRegistrations(builder.Build().Services));

        Assert.Contains(KubernetesClientSpark.Name, registration.Tags);
        Assert.Contains("custom-tag", registration.Tags);
    }

    [Fact]
    public void HealthCheck_HonorsFailureStatus_AndTimeout()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        var timeout = TimeSpan.FromSeconds(7);

        builder.IgniteKubernetesClient(
            configureSettings: settings =>
            {
                settings.HealthChecks.FailureStatus = HealthStatus.Degraded;
                settings.HealthChecks.Timeout = timeout;
            },
            kubernetesClientConfigurationFactory: _ => FakeConfig());

        var registration = Assert.Single(GetRegistrations(builder.Build().Services));

        Assert.Equal(HealthStatus.Degraded, registration.FailureStatus);
        Assert.Equal(timeout, registration.Timeout);
    }
}
