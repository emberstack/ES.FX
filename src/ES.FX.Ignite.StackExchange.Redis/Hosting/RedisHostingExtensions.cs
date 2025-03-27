using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.StackExchange.Redis.Configuration;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.StackExchangeRedis;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace ES.FX.Ignite.StackExchange.Redis.Hosting;

[PublicAPI]
public static class RedisHostingExtensions
{
    private static void ConfigureObservability(IHostApplicationBuilder builder, string? serviceKey,
        RedisSparkSettings settings)
    {
        if (settings.Tracing.Enabled)
        {
            builder.Services.AddOpenTelemetry().WithTracing(t =>
            {
                t.AddSource($"{typeof(StackExchangeRedisInstrumentation).Namespace}");
                t.ConfigureRedisInstrumentation(_ => { });
                t.AddInstrumentation(sp => sp.GetRequiredService<StackExchangeRedisInstrumentation>());
            });

            // Configure StackExchangeRedisInstrumentationOptions once 
            const string configureOnceKey = $"{RedisSpark.Name}.Global.Tracing.Configure";
            if (!builder.IsGuardConfigurationKeySet(configureOnceKey))
            {
                builder.GuardConfigurationKey(configureOnceKey);

                // Disable EnrichActivityWithTimingEvents as the activity is already timed
                // Enabling this leads to logs that get registered with each span
                builder.Services.AddOptions<StackExchangeRedisInstrumentationOptions>()
                    .Configure(s => s.EnrichActivityWithTimingEvents = false);
            }
        }

        if (settings.HealthChecks.Enabled)
        {
            var healthCheckName =
                $"{RedisSpark.Name}{(serviceKey is null ? string.Empty : $"[{serviceKey}]")}";
            builder.Services.AddHealthChecks().AddRedis(
                sp =>
                    serviceKey is null
                        ? sp.GetRequiredService<IConnectionMultiplexer>()
                        : sp.GetRequiredKeyedService<IConnectionMultiplexer>(serviceKey),
                healthCheckName,
                settings.HealthChecks.FailureStatus,
                [nameof(Redis), .. settings.HealthChecks.Tags],
                settings.HealthChecks.Timeout);
        }
    }

    /// <summary>
    ///     Registers <see cref="IConnectionMultiplexer" /> as a service in the services provided by the
    ///     <paramref name="builder" />.
    ///     Enables health check, logging and telemetry for the <see cref="IConnectionMultiplexer" />.
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
    /// <param name="configurationSectionPath">
    ///     The configuration section path. Default is
    ///     <see cref="RedisSpark.ConfigurationSectionPath" />.
    /// </param>
    public static void IgniteRedisClient(this IHostApplicationBuilder builder,
        string? name = null,
        string? serviceKey = null,
        Action<RedisSparkSettings>? configureSettings = null,
        Action<RedisSparkOptions>? configureOptions = null,
        string configurationSectionPath = RedisSpark.ConfigurationSectionPath)
    {
        builder.GuardConfigurationKey($"{RedisSpark.Name}[{serviceKey}]");

        var configPath = SparkConfig.Path(name, configurationSectionPath);

        var settings = SparkConfig.GetSettings(builder.Configuration, configPath, configureSettings);
        builder.Services.AddKeyedSingleton(serviceKey, settings);

        var optionsBuilder = builder.Services
            .AddOptions<RedisSparkOptions>(serviceKey ?? Options.DefaultName)
            .BindConfiguration(configPath);

        if (configureOptions is not null) optionsBuilder.Configure(configureOptions);




        builder.Services.AddKeyedSingleton<IConnectionMultiplexer>(serviceKey, (sp, _) =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<RedisSparkOptions>>()
                .Get(serviceKey ?? Options.DefaultName);

            var configOptions = !string.IsNullOrWhiteSpace(options.ConnectionString)
                ? ConfigurationOptions.Parse(options.ConnectionString)
                : options.ConfigurationOptions ?? new ConfigurationOptions();

            configOptions.LoggerFactory ??= sp.GetService<ILoggerFactory>();

            var connection = ConnectionMultiplexer.Connect(configOptions);

            var instanceSettings = sp.GetRequiredKeyedService<RedisSparkSettings>(serviceKey);

            if (instanceSettings.Tracing.Enabled)
            {
                sp.GetRequiredService<StackExchangeRedisInstrumentation>().AddConnection(connection);


            }

            return connection;
        });

        ConfigureObservability(builder, serviceKey, settings);
    }
}