using Azure.Storage.Queues;
using ES.FX.Ignite.Azure.Common.Hosting;
using ES.FX.Ignite.Azure.Storage.Queues.Configuration;
using ES.FX.Ignite.Spark.Configuration;
using HealthChecks.Azure.Storage.Queues;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Azure.Storage.Queues.Hosting;

[PublicAPI]
public static class AzureQueueStorageHostingExtensions
{
    /// <summary>
    ///     Registers <see cref="QueueServiceClient" /> as a service in the services provided by the
    ///     <paramref name="builder" />.
    ///     Enables health check, logging and telemetry for the <see cref="QueueServiceClient" />.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="name">A name used to retrieve the settings and options from configuration</param>
    /// <param name="serviceKey">
    ///     If not null, registers a keyed service with the service key. If null, registers a default
    ///     service
    /// </param>
    /// <param name="configureSettings">
    ///     An optional delegate that can be used for customizing settings. It's invoked after the
    ///     settings are read from the configuration.
    /// </param>
    /// <param name="configureClientOptions">
    ///     An optional delegate that can be used for customizing options. It's invoked after the
    ///     options are read from the configuration.
    /// </param>
    /// <param name="configurationSectionPath">
    ///     The configuration section path. Default is
    ///     <see cref="AzureQueueStorageSpark.ConfigurationSectionPath" />.
    /// </param>
    public static void IgniteAzureQueueServiceClient(this IHostApplicationBuilder builder,
        string? name = null,
        string? serviceKey = null,
        Action<AzureQueueStorageSparkSettings>? configureSettings = null,
        Action<QueueClientOptions>? configureClientOptions = null,
        string configurationSectionPath = AzureQueueStorageSpark.ConfigurationSectionPath)
    {
        builder.GuardConfigurationKey($"{AzureQueueStorageSpark.Name}-[{serviceKey}]");

        var configPath = SparkConfig.Path(name, configurationSectionPath);

        var settings = SparkConfig.GetSettings(builder.Configuration, configPath, configureSettings);
        builder.Services.AddKeyedSingleton(serviceKey, settings);

        builder.Services.IgniteAzureClient<QueueServiceClient, QueueClientOptions>(serviceKey,
            builder.Configuration.GetSection(configPath),
            configureClientOptions);

        builder.Services.IgniteAzureClientObservability<QueueServiceClient>(serviceKey,
            settings.TracingEnabled,
            settings.HealthChecksEnabled,
            (_, client) => new AzureQueueStorageHealthCheck(client));
    }
}