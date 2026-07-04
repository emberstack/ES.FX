using ES.FX.Ignite.Azure.Storage.Queues.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using static Xunit.Assert;

namespace ES.FX.Ignite.Azure.Storage.Queues.Tests;

/// <summary>
///     Confirms the observability wiring performed by
///     <see cref="AzureQueueStorageHostingExtensions.IgniteAzureQueueServiceClient" /> and the runtime behavior of the
///     registered <c>SimpleQueueServiceHealthCheck</c>. The health check is <c>internal</c>, so it is exercised through
///     the registered <see cref="HealthCheckService" /> against a real
///     <see cref="Azure.Storage.Queues.QueueServiceClient" />.
/// </summary>
public class HealthCheckWiringTests
{
    private static HostApplicationBuilder CreateBuilder(string? name, string? serviceKey,
        string connectionString = "UseDevelopmentStorage=true;")
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{AzureQueueStorageSpark.ConfigurationSectionPath}{(string.IsNullOrWhiteSpace(name) ? string.Empty : $":{name}")}:ConnectionString",
                connectionString)
        ]);
        return builder;
    }

    private static string ExpectedHealthCheckName(string? serviceKey) =>
        $"Azure-QueueServiceClient{(string.IsNullOrWhiteSpace(serviceKey) ? string.Empty : $"-[{serviceKey}]")}";

    [Theory]
    [InlineData(null, null)]
    [InlineData("default", null)]
    [InlineData("default", "keyed")]
    public void HealthCheck_Is_Registered_With_Expected_Name_And_Tags(string? name, string? serviceKey)
    {
        var builder = CreateBuilder(name, serviceKey);
        builder.IgniteAzureQueueServiceClient(name, serviceKey);

        var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        var registration = Single(options.Registrations,
            r => r.Name == ExpectedHealthCheckName(serviceKey));

        // The observability wiring tags the check with "Azure" and the client type name.
        Contains("Azure", registration.Tags);
        Contains("QueueServiceClient", registration.Tags);

        // The factory must produce the least-privilege page-list probe (SimpleQueueServiceHealthCheck),
        // not some other IHealthCheck. Resolving the registration exercises the factory against DI.
        var healthCheck = registration.Factory(app.Services);
        Equal("ES.FX.Ignite.Azure.Storage.Queues.HealthChecks.SimpleQueueServiceHealthCheck",
            healthCheck.GetType().FullName);
    }

    [Fact]
    public void HealthCheck_Is_Not_Registered_When_Disabled_Via_Configuration()
    {
        var builder = CreateBuilder(null, null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{AzureQueueStorageSpark.ConfigurationSectionPath}:Settings:HealthChecks:Enabled", "false")
        ]);

        builder.IgniteAzureQueueServiceClient();

        var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        Empty(options.Registrations);
    }

    [Fact]
    public void HealthCheck_Is_Not_Registered_When_Disabled_Via_ConfigureSettings_Delegate()
    {
        var builder = CreateBuilder(null, null);

        var delegateInvoked = false;
        builder.IgniteAzureQueueServiceClient(configureSettings: settings =>
        {
            delegateInvoked = true;
            settings.HealthChecks.Enabled = false;
        });

        var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        True(delegateInvoked);
        Empty(options.Registrations);
    }

    [Fact]
    public async Task CheckHealthAsync_Returns_FailureStatus_When_Queue_Probe_Throws()
    {
        // A well-formed but non-resolvable/unauthenticated endpoint: client construction succeeds, but the
        // GetQueuesAsync page probe throws, driving the catch branch of SimpleQueueServiceHealthCheck.
        var builder = CreateBuilder(null, null,
            "DefaultEndpointsProtocol=https;AccountName=esfxnonexistentaccount;" +
            "AccountKey=Zm9vYmFyYmF6cXV4Zm9vYmFyYmF6cXV4Zm9vYmFyYmF6cXV4Zm9vYmFyYmF6cXV4;" +
            "EndpointSuffix=core.windows.net");

        // Force a specific (non-default) failure status so we prove it is honored.
        builder.IgniteAzureQueueServiceClient(configureSettings: settings =>
        {
            settings.HealthChecks.FailureStatus = HealthStatus.Degraded;
            settings.HealthChecks.Timeout = TimeSpan.FromSeconds(30);
        });

        var app = builder.Build();
        var healthCheckService = app.Services.GetRequiredService<HealthCheckService>();

        var report = await healthCheckService.CheckHealthAsync(TestContext.Current.CancellationToken);

        var entry = report.Entries[ExpectedHealthCheckName(null)];
        Equal(HealthStatus.Degraded, entry.Status);
        NotNull(entry.Exception);
    }
}