using Azure.Core.Extensions;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Configuration.OpenTelemetry;
using JetBrains.Annotations;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Ignite.Azure.Common.Hosting;

[PublicAPI]
public static class AzureCommonHostingExtensions
{
    /// <summary>
    ///     Registers an Azure Client with the specified settings and options
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to read config from and add services to.</param>
    /// <param name="serviceKey">
    ///     If not null, registers a keyed service with the service key. If null, registers a default
    ///     service
    /// </param>
    /// <param name="configuration">Configuration for the client.</param>
    /// <param name="configureOptions">
    ///     An optional delegate that can be used for customizing options. It's invoked after the
    ///     options are read from the configuration.
    /// </param>
    /// <typeparam name="TClient">The type of the Azure client to register.</typeparam>
    /// <typeparam name="TOptions">The type of the Azure client options used to configure <typeparamref name="TClient" />.</typeparam>
    public static void IgniteAzureClient<TClient, TOptions>(this IServiceCollection services,
        string? serviceKey,
        IConfigurationSection configuration,
        Action<TOptions>? configureOptions = null)
        where TOptions : class
        where TClient : class
    {
        serviceKey = string.IsNullOrWhiteSpace(serviceKey) ? null : serviceKey;

        services.AddAzureClients(azureClientFactoryBuilder =>
        {
            var clientBuilder =
                ((IAzureClientFactoryBuilderWithConfiguration<IConfigurationSection>)azureClientFactoryBuilder)
                .RegisterClientFactory<TClient, TOptions>(configuration);

            if (serviceKey is not null) clientBuilder.WithName(serviceKey);
            clientBuilder.ConfigureOptions(options => configureOptions?.Invoke(options));
        });


        if (serviceKey is not null)
            services.AddKeyedSingleton(serviceKey,
                static (serviceProvider, serviceKey) => serviceProvider
                    .GetRequiredService<IAzureClientFactory<TClient>>().CreateClient((string)serviceKey!));
    }

    /// <summary>
    ///     Registers the observability for the Azure Client
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to read config from and add services to.</param>
    /// <param name="serviceKey">
    ///     If not null, registers a keyed service with the service key. If null, registers a default
    ///     service
    /// </param>
    /// <param name="tracingSettings">
    ///     <see cref="TracingSettings" />
    /// </param>
    /// <param name="healthCheckSettings">
    ///     <see cref="HealthCheckSettings" />
    /// </param>
    /// <param name="healthCheckFactory"> The factory used to create the health checks if enabled</param>
    /// <typeparam name="TClient">The type of the Azure client to register observability for.</typeparam>
    public static void IgniteAzureClientObservability<TClient>(this IServiceCollection services, string? serviceKey,
        TracingSettings tracingSettings,
        HealthCheckSettings healthCheckSettings,
        Func<IServiceProvider, TClient, IHealthCheck> healthCheckFactory) where TClient : class
    {
        serviceKey = string.IsNullOrWhiteSpace(serviceKey) ? null : serviceKey;

        if (tracingSettings.Enabled)
            services.AddOpenTelemetry().WithTracing(traceBuilder =>
                traceBuilder.AddSource($"{typeof(TClient).Namespace}.*"));

        if (healthCheckSettings.Enabled)
        {
            var healthCheckName =
                $"{nameof(Azure)}-{typeof(TClient).Name}{(serviceKey is null ? string.Empty : $"-[{serviceKey}]")}";
            services.AddHealthChecks().Add(new HealthCheckRegistration(healthCheckName,
                serviceProvider => healthCheckFactory(serviceProvider,
                    serviceProvider.GetRequiredKeyedService<TClient>(serviceKey)),
                healthCheckSettings.FailureStatus,
                [nameof(Azure), typeof(TClient).Name, .. healthCheckSettings.Tags],
                healthCheckSettings.Timeout));
        }
    }
}