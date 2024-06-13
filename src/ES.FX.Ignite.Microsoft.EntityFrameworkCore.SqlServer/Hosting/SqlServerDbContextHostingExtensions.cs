using EntityFramework.Exceptions.SqlServer;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Spark;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Configuration;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.HealthChecks;
using JetBrains.Annotations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Hosting;

[PublicAPI]
public static class SqlServerDbContextHostingExtensions
{
    /// <summary>
    ///     Registers the given <see cref="DbContext" /> as a service in the services provided by the
    ///     <paramref name="builder" />.
    ///     Enables retries, health check, logging and telemetry for the <see cref="DbContext" />.
    /// </summary>
    /// <typeparam name="TDbContext">The <see cref="TDbContext" /> that needs to be registered.</typeparam>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="key">A name used to retrieve the settings and options from configuration</param>
    /// <param name="configureSettings">
    ///     An optional delegate that can be used for customizing settings. It's invoked after the
    ///     settings are read from the configuration.
    /// </param>
    /// <param name="configureOptions">
    ///     An optional delegate that can be used for customizing options. It's invoked after the
    ///     options are read from the configuration.
    /// </param>
    /// <param name="configureDbContextOptionsBuilder">
    ///     An optional delegate that can be used for customizing the
    ///     <see cref="DbContextOptionsBuilder" />.
    /// </param>
    /// <param name="configureSqlServerDbContextOptionsBuilder">
    ///     An optional delegate that can be used for customizing the
    ///     <see cref="SqlServerDbContextOptionsBuilder" />.
    /// </param>
    /// <param name="lifetime">
    ///     o
    ///     The lifetime of the <see cref="DbContext" />. Default is
    ///     <see cref="ServiceLifetime.Transient" />.
    /// </param>
    /// <param name="configurationSectionKey">
    ///     The configuration section key. Default is
    ///     <see cref="DbContextSpark.ConfigurationSectionKey" />.
    /// </param>
    public static void AddSqlServerDbContext<TDbContext>(this IHostApplicationBuilder builder,
        string? key = null,
        Action<SqlServerDbContextSparkSettings<TDbContext>>? configureSettings = null,
        Action<SqlServerDbContextSparkOptions<TDbContext>>? configureOptions = null,
        Action<DbContextOptionsBuilder>? configureDbContextOptionsBuilder = null,
        Action<SqlServerDbContextOptionsBuilder>? configureSqlServerDbContextOptionsBuilder = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient,
        string configurationSectionKey = DbContextSpark.ConfigurationSectionKey) where TDbContext : DbContext =>
        builder.RegisterDbContext(key, configureSettings, configureOptions,
            configureDbContextOptionsBuilder, configureSqlServerDbContextOptionsBuilder, lifetime,
            configurationSectionKey);

    /// <summary>
    ///     Registers a <see cref="IDbContextFactory{TDbContext}" /> as a service in the services provided by the
    ///     <paramref name="builder" />.
    ///     Enables retries, health check, logging and telemetry for the <see cref="DbContext" />.
    /// </summary>
    /// <typeparam name="TDbContext">
    ///     The <see cref="TDbContext" /> type used by the
    ///     <see cref="IDbContextFactory{TDbContext}" /> that needs to be registered.
    /// </typeparam>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="key">A name used to retrieve the settings and options from configuration</param>
    /// <param name="configureSettings">
    ///     An optional delegate that can be used for customizing settings. It's invoked after the
    ///     settings are read from the configuration.
    /// </param>
    /// <param name="configureOptions">
    ///     An optional delegate that can be used for customizing options. It's invoked after the
    ///     options are read from the configuration.
    /// </param>
    /// <param name="configureDbContextOptionsBuilder">
    ///     An optional delegate that can be used for customizing the
    ///     <see cref="DbContextOptionsBuilder" />.
    /// </param>
    /// <param name="configureSqlServerDbContextOptionsBuilder">
    ///     An optional delegate that can be used for customizing the
    ///     <see cref="SqlServerDbContextOptionsBuilder" />.
    /// </param>
    /// <param name="lifetime">
    ///     The lifetime of the <see cref="IDbContextFactory{TDbContext}" />. Default is
    ///     <see cref="ServiceLifetime.Transient" />.
    /// </param>
    /// <param name="configurationSectionKey">
    ///     The configuration section key. Default is
    ///     <see cref="DbContextSpark.ConfigurationSectionKey" />.
    /// </param>
    /// <remarks>
    ///     This also registers the <see cref="DbContext" /> as a service in the services provided by the
    ///     <paramref name="builder" /> with the same lifetime specified by <paramref name="lifetime" />.
    /// </remarks>
    public static void AddSqlServerDbContextFactory<TDbContext>(this IHostApplicationBuilder builder,
        string? key = null,
        Action<SqlServerDbContextSparkSettings<TDbContext>>? configureSettings = null,
        Action<SqlServerDbContextSparkOptions<TDbContext>>? configureOptions = null,
        Action<DbContextOptionsBuilder>? configureDbContextOptionsBuilder = null,
        Action<SqlServerDbContextOptionsBuilder>? configureSqlServerDbContextOptionsBuilder = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient,
        string configurationSectionKey = DbContextSpark.ConfigurationSectionKey) where TDbContext : DbContext =>
        builder.RegisterDbContext(key, configureSettings, configureOptions,
            configureDbContextOptionsBuilder, configureSqlServerDbContextOptionsBuilder, lifetime,
            configurationSectionKey, true);


    private static void RegisterDbContext<TDbContext>(this IHostApplicationBuilder builder,
        string? key = null,
        Action<SqlServerDbContextSparkSettings<TDbContext>>? configureSettings = null,
        Action<SqlServerDbContextSparkOptions<TDbContext>>? configureOptions = null,
        Action<DbContextOptionsBuilder>? configureDbContextOptionsBuilder = null,
        Action<SqlServerDbContextOptionsBuilder>? configureSqlServerDbContextOptionsBuilder = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient,
        string configurationSectionKey = DbContextSpark.ConfigurationSectionKey,
        bool useDbContextFactory = false) where TDbContext : DbContext
    {
        key = SparkConfig.Key(key, typeof(TDbContext).Name);
        var configurationKey = SparkConfig.ConfigurationKey(key, configurationSectionKey);

        ConfigureOptions(builder, configurationKey, configureOptions);

        var settings = SparkConfig.GetSettings(builder.Configuration, configurationKey, configureSettings);
        builder.Services.AddSingleton(settings);

        if (useDbContextFactory)
            builder.Services.AddDbContextFactory<TDbContext>(ConfigureBuilder, lifetime);
        else
            builder.Services.AddDbContext<TDbContext>(ConfigureBuilder, lifetime);

        ConfigureInstrumentation(builder, key, settings);


        return;

        void ConfigureBuilder(IServiceProvider sp, DbContextOptionsBuilder dbContextOptionsBuilder)
        {
            ConfigureDbContextOptionsBuilder<TDbContext>(sp, dbContextOptionsBuilder,
                configureDbContextOptionsBuilder,
                configureSqlServerDbContextOptionsBuilder);
        }
    }


    private static void ConfigureOptions<T>(
        IHostApplicationBuilder builder,
        string configurationKey,
        Action<SqlServerDbContextSparkOptions<T>>? configureOptions = null) where T : DbContext
    {
        var optionsBuilder = builder.Services
            .AddOptions<SqlServerDbContextSparkOptions<T>>()
            .BindConfiguration(configurationKey);

        if (configureOptions is not null) optionsBuilder.Configure(configureOptions);
    }

    private static void ConfigureDbContextOptionsBuilder<T>(IServiceProvider serviceProvider,
        DbContextOptionsBuilder dbContextOptionsBuilder,
        Action<DbContextOptionsBuilder>? configureDbContextOptionsBuilder,
        Action<SqlServerDbContextOptionsBuilder>? configureSqlServerDbContextOptionsBuilder) where T : DbContext
    {
        var sqlServerDbContextSparkOptions = serviceProvider
            .GetRequiredService<IOptionsMonitor<SqlServerDbContextSparkOptions<T>>>()
            .CurrentValue;

        var connectionStringBuilder = new SqlConnectionStringBuilder(sqlServerDbContextSparkOptions.ConnectionString);

        dbContextOptionsBuilder
            .UseSqlServer(connectionStringBuilder.ConnectionString, options =>
            {
                if (!sqlServerDbContextSparkOptions.DisableRetry) options.EnableRetryOnFailure();

                if (sqlServerDbContextSparkOptions.CommandTimeout.HasValue)
                    options.CommandTimeout(sqlServerDbContextSparkOptions.CommandTimeout.Value);

                configureSqlServerDbContextOptionsBuilder?.Invoke(options);
            })
            .UseExceptionProcessor();
        configureDbContextOptionsBuilder?.Invoke(dbContextOptionsBuilder);
    }


    private static void ConfigureInstrumentation<TContext>(
        IHostApplicationBuilder builder,
        string key,
        SqlServerDbContextSparkSettings<TContext> settings) where TContext : DbContext
    {
        if (!settings.DisableTracing)
            builder.Services.AddOpenTelemetry().WithTracing(tracerProviderBuilder =>
                tracerProviderBuilder.AddSqlClientInstrumentation());

        if (!settings.DisableHealthChecks)
            builder.TryAddHealthCheck(
                $"{DbContextSpark.Name}.{key}",
                static hcBuilder => hcBuilder.AddDbContextCheck<TContext>());
    }
}