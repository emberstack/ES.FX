using ES.FX.Additions.KubernetesClient.HealthChecks;
using ES.FX.Ignite.KubernetesClient.Configuration;
using ES.FX.Ignite.Spark.Configuration;
using JetBrains.Annotations;
using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ES.FX.Ignite.KubernetesClient.Hosting;

[PublicAPI]
public static class KubernetesClientHostingExtensions
{
    /// <summary>
    ///     Registers <see cref="IKubernetes" /> as a service in the services provided by the
    ///     <paramref name="builder" />.
    ///     Enables health check, logging and telemetry for the <see cref="IKubernetes" />.
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
    /// <param name="configureOptions">
    ///     An optional delegate that can be used for customizing options. It's invoked after the
    ///     options are read from the configuration.
    /// </param>
    /// <param name="kubernetesClientConfigurationFactory">
    ///     An optional delegate that can be used for creating the <see cref="KubernetesClientConfiguration" />.
    ///     By default <see cref="KubernetesClientConfiguration.BuildDefaultConfig" /> is used.
    /// </param>
    /// <param name="configureKubernetesClientConfiguration">
    ///     An optional delegate that can be used for customizing the <see cref="KubernetesClientConfiguration" />.
    /// </param>
    /// <param name="kubernetesClientDelegatingHandlers">
    ///     Optional delegating handlers to add to the http client pipeline.
    /// </param>
    /// <param name="lifetime">
    ///     The lifetime of the <see cref="IKubernetes" />. Default is
    ///     <see cref="ServiceLifetime.Singleton" />.
    /// </param>
    /// <param name="configurationSectionPath">
    ///     The configuration section path. Default is
    ///     <see cref="KubernetesClientSpark.ConfigurationSectionPath" />.
    /// </param>
    public static void IgniteKubernetesClient(this IHostApplicationBuilder builder,
        string? name = null,
        string? serviceKey = null,
        Action<KubernetesClientSparkSettings>? configureSettings = null,
        Action<KubernetesClientSparkOptions>? configureOptions = null,
        Func<IServiceProvider, KubernetesClientConfiguration>? kubernetesClientConfigurationFactory = null,
        Action<IServiceProvider, KubernetesClientConfiguration>? configureKubernetesClientConfiguration = null,
        DelegatingHandler[]? kubernetesClientDelegatingHandlers = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton,
        string configurationSectionPath = KubernetesClientSpark.ConfigurationSectionPath)
    {
        builder.GuardConfigurationKey($"{KubernetesClientSpark.Name}[{serviceKey}]");

        var configPath = SparkConfig.Path(name, configurationSectionPath);

        var settings = SparkConfig.GetSettings(builder.Configuration, configPath, configureSettings);
        builder.Services.AddKeyedSingleton(serviceKey, settings);

        var optionsBuilder = builder.Services
            .AddOptions<KubernetesClientSparkOptions>(serviceKey ?? Options.DefaultName)
            .BindConfiguration(configPath);

        if (configureOptions is not null) optionsBuilder.Configure(configureOptions);


        builder.Services.Add(new ServiceDescriptor(typeof(KubernetesClientConfiguration), serviceKey,
            (sp, key) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<KubernetesClientSparkOptions>>().Get(key as string);
                var clientConfiguration = kubernetesClientConfigurationFactory?.Invoke(sp) ??
                                          KubernetesClientConfiguration.BuildDefaultConfig();

                clientConfiguration.SkipTlsVerify = options.SkipTlsVerify;

                configureKubernetesClientConfiguration?.Invoke(sp, clientConfiguration);
                return clientConfiguration;
            }, lifetime));
        builder.Services.Add(new ServiceDescriptor(typeof(IKubernetes), serviceKey, (sp, key) =>
        {
            var clientConfiguration =
                sp.GetRequiredKeyedService<KubernetesClientConfiguration>(key as string);
            var client = new Kubernetes(clientConfiguration, kubernetesClientDelegatingHandlers);
            return client;
        }, lifetime));


        ConfigureObservability(builder, serviceKey, settings);
    }


    private static void ConfigureObservability(IHostApplicationBuilder builder, string? serviceKey,
        KubernetesClientSparkSettings settings)
    {
        if (settings.HealthChecks.Enabled)
        {
            var healthCheckName =
                $"{KubernetesClientSpark.Name}{(string.IsNullOrWhiteSpace(serviceKey) ? string.Empty : $"[{serviceKey}]")}";
            builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(healthCheckName, serviceProvider =>
                {
                    var client = serviceProvider.GetRequiredKeyedService<IKubernetes>(serviceKey);
                    return new KubernetesHealthCheck(client);
                },
                settings.HealthChecks.FailureStatus,
                [KubernetesClientSpark.Name, .. settings.HealthChecks.Tags],
                settings.HealthChecks.Timeout));
        }
    }
}