using ES.FX.Ignite.Microsoft.Data.SqlClient.Configuration;
using ES.FX.Ignite.Microsoft.Data.SqlClient.Spark;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.HealthChecks;
using ES.FX.Microsoft.Data.SqlClient.Abstractions;
using ES.FX.Microsoft.Data.SqlClient.Factories;
using HealthChecks.SqlServer;
using JetBrains.Annotations;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;

namespace ES.FX.Ignite.Microsoft.Data.SqlClient.Hosting;

[PublicAPI]
public static class SqlServerClientHostingExtensions
{
    /// <summary>
    ///     Registers <see cref="SqlConnection" /> as a service in the services provided by the
    ///     <paramref name="builder" />.
    ///     Enables health check, logging and telemetry for the <see cref="SqlConnection" />.
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
    /// <param name="lifetime">
    ///     The lifetime of the <see cref="SqlConnection" />. Default is
    ///     <see cref="ServiceLifetime.Transient" />.
    /// </param>
    /// <param name="configurationSectionPath">
    ///     The configuration section path. Default is
    ///     <see cref="SqlServerClientSpark.ConfigurationSectionPath" />.
    /// </param>
    public static void AddSqlServerClient(this IHostApplicationBuilder builder,
        string name,
        string? serviceKey = null,
        Action<SqlServerClientSparkSettings>? configureSettings = null,
        Action<SqlServerClientSparkOptions>? configureOptions = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient,
        string configurationSectionPath = SqlServerClientSpark.ConfigurationSectionPath) =>
        RegisterSqlServerClient(builder, name, serviceKey, configureSettings, configureOptions, lifetime,
            configurationSectionPath);


    /// <summary>
    ///     Registers <see cref="ISqlConnectionFactory" /> as a service in the services provided by the
    ///     <paramref name="builder" />.
    ///     Enables health check, logging and telemetry for <see cref="SqlConnection" />.
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
    /// <param name="lifetime">
    ///     The lifetime of the <see cref="ISqlConnectionFactory" />. Default is
    ///     <see cref="ServiceLifetime.Transient" />.
    /// </param>
    /// <param name="configurationSectionPath">
    ///     The configuration section path. Default is
    ///     <see cref="SqlServerClientSpark.ConfigurationSectionPath" />.
    /// </param>
    /// <remarks> For convenience, this method also registers <see cref="SqlConnection" /> as a service.</remarks>
    public static void AddSqlServerClientFactory(this IHostApplicationBuilder builder,
        string name,
        string? serviceKey = null,
        Action<SqlServerClientSparkSettings>? configureSettings = null,
        Action<SqlServerClientSparkOptions>? configureOptions = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient,
        string configurationSectionPath = SqlServerClientSpark.ConfigurationSectionPath) =>
        RegisterSqlServerClient(builder, name, serviceKey, configureSettings, configureOptions, lifetime,
            configurationSectionPath, true);


    private static void RegisterSqlServerClient(this IHostApplicationBuilder builder,
        string name,
        string? serviceKey = null,
        Action<SqlServerClientSparkSettings>? configureSettings = null,
        Action<SqlServerClientSparkOptions>? configureOptions = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient,
        string configurationSectionPath = SqlServerClientSpark.ConfigurationSectionPath,
        bool useFactory = false)
    {
        var configPath = SparkConfig.Path(name, configurationSectionPath);

        var settings = SparkConfig.GetSettings(builder.Configuration, configPath, configureSettings);
        builder.Services.AddKeyedSingleton(serviceKey, settings);

        var optionsBuilder = builder.Services
            .AddOptions<SqlServerClientSparkOptions>(serviceKey ?? Options.DefaultName)
            .BindConfiguration(configPath);
        if (configureOptions is not null) optionsBuilder.Configure(configureOptions);


        if (useFactory)
            builder.Services.Add(new ServiceDescriptor(typeof(ISqlConnectionFactory), serviceKey,
                (provider, key) => new DelegateSqlConnectionFactory(provider, sp => ResolveSqlConnection(sp, key)),
                lifetime));

        builder.Services.Add(new ServiceDescriptor(typeof(SqlConnection), serviceKey, ResolveSqlConnection, lifetime));


        if (!settings.DisableTracing)
            builder.Services.AddOpenTelemetry().WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder.AddSqlClientInstrumentation();
            });

        if (!settings.DisableHealthChecks)
            builder.TryAddHealthCheck(new HealthCheckRegistration(
                $"{SqlServerClientSpark.Name}_{name.Trim()}" +
                (string.IsNullOrWhiteSpace(serviceKey) ? string.Empty : $"_{serviceKey.Trim()}")
                ,
                sp =>
                {
                    var options = sp.GetRequiredService<IOptionsMonitor<SqlServerClientSparkOptions>>().Get(serviceKey);
                    return new SqlServerHealthCheck(new SqlServerHealthCheckOptions
                    {
                        ConnectionString = options.ConnectionString ?? string.Empty
                    });
                },
                default,
                default,
                default));

        return;

        static SqlConnection ResolveSqlConnection(IServiceProvider sp, object? key)
        {
            var options = sp.GetRequiredService<IOptionsMonitor<SqlServerClientSparkOptions>>().Get(key as string);
            return new SqlConnection(options.ConnectionString);
        }
    }
}