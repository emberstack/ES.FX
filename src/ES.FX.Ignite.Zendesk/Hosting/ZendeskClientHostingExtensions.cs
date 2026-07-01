using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Zendesk.Configuration;
using ES.FX.Ignite.Zendesk.HealthChecks;
using ES.FX.Zendesk;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Configuration;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ES.FX.Ignite.Zendesk.Hosting;

/// <summary>
///     Hosting extensions that wire the Zendesk API client into Ignite. Supports multiple named/keyed instances.
/// </summary>
[PublicAPI]
public static class ZendeskClientHostingExtensions
{
    /// <summary>
    ///     Registers <see cref="IZendeskClient" /> (a typed <see cref="HttpClient" />) with config binding,
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
    ///     The configuration section path. Default is <see cref="ZendeskClientSpark.ConfigurationSectionPath" />.
    /// </param>
    /// <returns>The <see cref="IHttpClientBuilder" /> for the underlying named client, for further customization.</returns>
    public static IHttpClientBuilder IgniteZendeskClient(this IHostApplicationBuilder builder,
        string? name = null,
        string? serviceKey = null,
        Action<ZendeskClientSparkSettings>? configureSettings = null,
        Action<ZendeskClientOptions>? configureOptions = null,
        string configurationSectionPath = ZendeskClientSpark.ConfigurationSectionPath)
    {
        builder.GuardConfigurationKey($"{ZendeskClientSpark.Name}[{serviceKey}]");

        var configPath = SparkConfig.Path(name, configurationSectionPath);

        var settings = SparkConfig.GetSettings(builder.Configuration, configPath, configureSettings);
        builder.Services.AddKeyedSingleton(serviceKey, settings);

        var optionsBuilder = builder.Services
            .AddOptions<ZendeskClientOptions>(serviceKey ?? Options.DefaultName)
            .BindConfiguration(configPath)
            .ValidateOnStart();
        if (configureOptions is not null) optionsBuilder.Configure(configureOptions);

        var httpClientBuilder = builder.Services.AddZendeskClient(serviceKey);

        ConfigureObservability(builder, serviceKey, settings);

        return httpClientBuilder;
    }

    private static void ConfigureObservability(IHostApplicationBuilder builder, string? serviceKey,
        ZendeskClientSparkSettings settings)
    {
        if (settings.Tracing.Enabled)
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing.AddSource(ZendeskClientInstrumentation.ActivitySourceName));

        if (settings.HealthChecks.Enabled)
        {
            var healthCheckName =
                $"{ZendeskClientSpark.Name}{(serviceKey is null ? string.Empty : $"[{serviceKey}]")}";
            builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
                healthCheckName,
                sp => new ZendeskClientHealthCheck(serviceKey is null
                    ? sp.GetRequiredService<IZendeskClient>()
                    : sp.GetRequiredKeyedService<IZendeskClient>(serviceKey)),
                settings.HealthChecks.FailureStatus,
                ["Zendesk", .. settings.HealthChecks.Tags],
                settings.HealthChecks.Timeout));
        }
    }
}