using ES.FX.Ignite.NousResearch.HermesAgent.Configuration;
using ES.FX.Ignite.NousResearch.HermesAgent.HealthChecks;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.NousResearch.HermesAgent;
using ES.FX.NousResearch.HermesAgent.Abstractions;
using ES.FX.NousResearch.HermesAgent.Configuration;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ES.FX.Ignite.NousResearch.HermesAgent.Hosting;

/// <summary>
///     Hosting extensions that wire the Hermes Agent API client into Ignite. Supports multiple named/keyed
///     instances.
/// </summary>
[PublicAPI]
public static class HermesAgentClientHostingExtensions
{
    /// <summary>
    ///     Registers <see cref="IHermesAgentClient" /> (a typed <see cref="HttpClient" />) with config binding,
    ///     options validation, a live health check and OpenTelemetry tracing.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="name">A name used to retrieve the settings and options from configuration (for multiple instances).</param>
    /// <param name="serviceKey">
    ///     If not <c>null</c>, registers a keyed service with the service key. If <c>null</c>, registers the default
    ///     service.
    /// </param>
    /// <param name="configureSettings">
    ///     An optional delegate to customize settings. Invoked after settings are read from configuration.
    /// </param>
    /// <param name="configureOptions">
    ///     An optional delegate to customize options. Invoked after options are read from configuration.
    /// </param>
    /// <param name="configurationSectionPath">
    ///     The configuration section path. Default is <see cref="HermesAgentClientSpark.ConfigurationSectionPath" />.
    /// </param>
    /// <returns>The <see cref="IHttpClientBuilder" /> for the underlying named client, for further customization.</returns>
    public static IHttpClientBuilder IgniteHermesAgentClient(this IHostApplicationBuilder builder,
        string? name = null,
        string? serviceKey = null,
        Action<HermesAgentClientSparkSettings>? configureSettings = null,
        Action<HermesAgentClientOptions>? configureOptions = null,
        string configurationSectionPath = HermesAgentClientSpark.ConfigurationSectionPath)
    {
        builder.GuardConfigurationKey($"{HermesAgentClientSpark.Name}[{serviceKey}]");

        var configPath = SparkConfig.Path(name, configurationSectionPath);

        var settings = SparkConfig.GetSettings(builder.Configuration, configPath, configureSettings);
        builder.Services.AddKeyedSingleton(serviceKey, settings);

        var optionsBuilder = builder.Services
            .AddOptions<HermesAgentClientOptions>(serviceKey ?? Options.DefaultName)
            .BindConfiguration(configPath)
            .ValidateOnStart();
        if (configureOptions is not null) optionsBuilder.Configure(configureOptions);

        var httpClientBuilder = builder.Services.AddHermesAgentClient(serviceKey);

        ConfigureObservability(builder, serviceKey, settings);

        return httpClientBuilder;
    }

    private static void ConfigureObservability(IHostApplicationBuilder builder, string? serviceKey,
        HermesAgentClientSparkSettings settings)
    {
        if (settings.Tracing.Enabled)
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing.AddSource(HermesAgentClientInstrumentation.ActivitySourceName));

        if (settings.HealthChecks.Enabled)
        {
            var healthCheckName =
                $"{HermesAgentClientSpark.Name}{(serviceKey is null ? string.Empty : $"[{serviceKey}]")}";
            builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
                healthCheckName,
                sp => new HermesAgentClientHealthCheck(serviceKey is null
                    ? sp.GetRequiredService<IHermesAgentClient>()
                    : sp.GetRequiredKeyedService<IHermesAgentClient>(serviceKey)),
                settings.HealthChecks.FailureStatus,
                ["HermesAgent", .. settings.HealthChecks.Tags],
                settings.HealthChecks.Timeout));
        }
    }
}