using Azure.Storage.Queues;
using ES.FX.Ignite.Azure.Storage.Queues.Configuration;
using ES.FX.Ignite.Azure.Storage.Queues.Hosting;
using ES.FX.Ignite.Spark.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using static Xunit.Assert;

namespace ES.FX.Ignite.Azure.Storage.Queues.Tests;

/// <summary>
///     Confirms the registration surface of
///     <see cref="AzureQueueStorageHostingExtensions.IgniteAzureQueueServiceClient" />: the duplicate-registration guard,
///     configuration binding of settings, the <c>configureSettings</c> mutation delegate, and the
///     <c>configureClientOptions</c> delegate.
/// </summary>
public class SparkRegistrationTests
{
    private static HostApplicationBuilder CreateBuilder()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{AzureQueueStorageSpark.ConfigurationSectionPath}:ConnectionString",
                "UseDevelopmentStorage=true;")
        ]);
        return builder;
    }

    [Fact]
    public void Second_Registration_With_Same_ServiceKey_Throws()
    {
        var builder = CreateBuilder();

        builder.IgniteAzureQueueServiceClient();

        var ex = Throws<ReconfigurationNotSupportedException>(() =>
            builder.IgniteAzureQueueServiceClient());
        Contains(AzureQueueStorageSpark.Name, ex.Message);
    }

    [Fact]
    public void Second_Registration_With_Different_ServiceKey_Does_Not_Throw()
    {
        var builder = CreateBuilder();

        builder.IgniteAzureQueueServiceClient(serviceKey: "one");
        builder.IgniteAzureQueueServiceClient(serviceKey: "two");

        var app = builder.Build();
        NotNull(app.Services.GetKeyedService<QueueServiceClient>("one"));
        NotNull(app.Services.GetKeyedService<QueueServiceClient>("two"));
    }

    [Fact]
    public void Settings_Are_Bound_From_Configuration_Section()
    {
        var builder = CreateBuilder();
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{AzureQueueStorageSpark.ConfigurationSectionPath}:Settings:HealthChecks:Enabled", "false"),
            new KeyValuePair<string, string?>(
                $"{AzureQueueStorageSpark.ConfigurationSectionPath}:Settings:Tracing:Enabled", "false")
        ]);

        builder.IgniteAzureQueueServiceClient();

        var app = builder.Build();
        var settings = app.Services.GetRequiredService<AzureQueueStorageSparkSettings>();

        False(settings.HealthChecks.Enabled);
        False(settings.Tracing.Enabled);
    }

    [Fact]
    public void Settings_Defaults_Are_Enabled_When_Not_Configured()
    {
        var builder = CreateBuilder();
        builder.IgniteAzureQueueServiceClient();

        var app = builder.Build();
        var settings = app.Services.GetRequiredService<AzureQueueStorageSparkSettings>();

        True(settings.HealthChecks.Enabled);
        True(settings.Tracing.Enabled);
    }

    [Fact]
    public void ConfigureSettings_Delegate_Runs_After_Configuration_Binding()
    {
        var builder = CreateBuilder();
        // Configuration enables health checks; the delegate must win because it runs after binding.
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{AzureQueueStorageSpark.ConfigurationSectionPath}:Settings:HealthChecks:Enabled", "true")
        ]);

        AzureQueueStorageSparkSettings? observed = null;
        builder.IgniteAzureQueueServiceClient(configureSettings: settings =>
        {
            // Prove the bound value is visible to the delegate before we override it.
            observed = settings;
            True(settings.HealthChecks.Enabled);
            settings.HealthChecks.Enabled = false;
        });

        var app = builder.Build();
        var settings = app.Services.GetRequiredService<AzureQueueStorageSparkSettings>();

        NotNull(observed);
        Same(observed, settings);
        False(settings.HealthChecks.Enabled);
    }

    [Fact]
    public void ConfigureClientOptions_Delegate_Is_Invoked_And_Applied()
    {
        var builder = CreateBuilder();

        var invoked = false;
        builder.IgniteAzureQueueServiceClient(configureClientOptions: options =>
        {
            invoked = true;
            options.MessageEncoding = QueueMessageEncoding.Base64;
        });

        var app = builder.Build();

        // Resolving the client forces the Azure client factory to materialize options, invoking the delegate.
        var client = app.Services.GetRequiredService<QueueServiceClient>();
        NotNull(client);
        True(invoked);
    }

    [Fact]
    public void Tracing_Source_Is_Registered_When_Enabled()
    {
        var builder = CreateBuilder();
        builder.IgniteAzureQueueServiceClient();

        var app = builder.Build();

        // When tracing is enabled the Spark calls AddOpenTelemetry().WithTracing(...), which registers a
        // TracerProvider in the container. Resolving it confirms the tracing branch ran.
        var tracerProvider = app.Services.GetService<TracerProvider>();
        NotNull(tracerProvider);
    }

    [Fact]
    public void Tracing_Source_Is_Not_Registered_When_Disabled()
    {
        var builder = CreateBuilder();
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{AzureQueueStorageSpark.ConfigurationSectionPath}:Settings:Tracing:Enabled", "false")
        ]);

        builder.IgniteAzureQueueServiceClient();

        var app = builder.Build();

        // With tracing disabled the Spark never calls AddOpenTelemetry().WithTracing(...), so no TracerProvider
        // is registered. This proves the enabled-branch assertion above is load-bearing.
        var tracerProvider = app.Services.GetService<TracerProvider>();
        Null(tracerProvider);
    }
}