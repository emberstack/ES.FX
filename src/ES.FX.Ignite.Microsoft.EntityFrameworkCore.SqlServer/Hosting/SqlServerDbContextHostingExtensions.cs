using EntityFramework.Exceptions.SqlServer;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Configuration;
using ES.FX.Ignite.Spark;
using ES.FX.Ignite.Spark.Configuration;
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
    ///     Registers <see cref="TDbContext" /> as a service in the services provided by the
    ///     <paramref name="builder" />.
    ///     Enables retries, health check, logging and telemetry for the <see cref="TDbContext" />.
    /// </summary>
    /// <typeparam name="TDbContext">The <see cref="TDbContext" /> that needs to be registered.</typeparam>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="name">A name used to retrieve the settings and options from configuration</param>
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
    ///     The lifetime of the <see cref="DbContext" />. Default is
    ///     <see cref="ServiceLifetime.Transient" />.
    /// </param>
    /// <param name="configurationSectionPath">
    ///     The configuration section path. Default is
    ///     <see cref="DbContextSpark.ConfigurationSectionPath" />.
    /// </param>
    public static void AddIgniteSqlServerDbContext<TDbContext>(this IHostApplicationBuilder builder,
        string? name = null,
        Action<SqlServerDbContextSparkSettings<TDbContext>>? configureSettings = null,
        Action<SqlServerDbContextSparkOptions<TDbContext>>? configureOptions = null,
        Action<DbContextOptionsBuilder>? configureDbContextOptionsBuilder = null,
        Action<SqlServerDbContextOptionsBuilder>? configureSqlServerDbContextOptionsBuilder = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient,
        string configurationSectionPath = DbContextSpark.ConfigurationSectionPath) where TDbContext : DbContext =>
        builder.AddSqlServerDbContext(name, configureSettings, configureOptions,
            configureDbContextOptionsBuilder, configureSqlServerDbContextOptionsBuilder, lifetime,
            configurationSectionPath);

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
    /// <param name="name">A name used to retrieve the settings and options from configuration</param>
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
    /// <param name="configurationSectionPath">
    ///     The configuration section path. Default is
    ///     <see cref="DbContextSpark.ConfigurationSectionPath" />.
    /// </param>
    /// <remarks>
    ///     This also registers the <see cref="DbContext" /> as a service in the services provided by the
    ///     <paramref name="builder" /> with the same lifetime specified by <paramref name="lifetime" />.
    /// </remarks>
    public static void AddIgniteSqlServerDbContextFactory<TDbContext>(this IHostApplicationBuilder builder,
        string? name = null,
        Action<SqlServerDbContextSparkSettings<TDbContext>>? configureSettings = null,
        Action<SqlServerDbContextSparkOptions<TDbContext>>? configureOptions = null,
        Action<DbContextOptionsBuilder>? configureDbContextOptionsBuilder = null,
        Action<SqlServerDbContextOptionsBuilder>? configureSqlServerDbContextOptionsBuilder = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient,
        string configurationSectionPath = DbContextSpark.ConfigurationSectionPath) where TDbContext : DbContext =>
        builder.AddSqlServerDbContext(name, configureSettings, configureOptions,
            configureDbContextOptionsBuilder, configureSqlServerDbContextOptionsBuilder, lifetime,
            configurationSectionPath, true);


    private static void AddSqlServerDbContext<TDbContext>(this IHostApplicationBuilder builder,
        string? name = null,
        Action<SqlServerDbContextSparkSettings<TDbContext>>? configureSettings = null,
        Action<SqlServerDbContextSparkOptions<TDbContext>>? configureOptions = null,
        Action<DbContextOptionsBuilder>? configureDbContextOptionsBuilder = null,
        Action<SqlServerDbContextOptionsBuilder>? configureSqlServerDbContextOptionsBuilder = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient,
        string configurationSectionPath = DbContextSpark.ConfigurationSectionPath,
        bool useFactory = false) where TDbContext : DbContext
    {
        builder.GuardSparkConfiguration($"{DbContextSpark.Name}[{typeof(TDbContext).FullName}]",
            $"{DbContextSpark.Name}[{typeof(TDbContext).FullName}] already configured.");

        name = SparkConfig.Name(name, typeof(TDbContext).Name);
        var configPath = SparkConfig.Path(name, configurationSectionPath);

        var settings = SparkConfig.GetSettings(builder.Configuration, configPath, configureSettings);
        builder.Services.AddSingleton(settings);

        var optionsBuilder = builder.Services
            .AddOptions<SqlServerDbContextSparkOptions<TDbContext>>()
            .BindConfiguration(configPath);
        if (configureOptions is not null) optionsBuilder.Configure(configureOptions);

        if (useFactory)
            builder.Services.AddDbContextFactory<TDbContext>(ConfigureBuilder, lifetime);
        else
            builder.Services.AddDbContext<TDbContext>(ConfigureBuilder, lifetime);

        ConfigureObservability(builder, name, settings);


        return;

        void ConfigureBuilder(IServiceProvider sp, DbContextOptionsBuilder dbContextOptionsBuilder) =>
            ConfigureDbContextOptionsBuilder<TDbContext>(sp, dbContextOptionsBuilder,
                configureDbContextOptionsBuilder,
                configureSqlServerDbContextOptionsBuilder);
    }


    private static void ConfigureDbContextOptionsBuilder<TDbContext>(IServiceProvider serviceProvider,
        DbContextOptionsBuilder dbContextOptionsBuilder,
        Action<DbContextOptionsBuilder>? configureDbContextOptionsBuilder,
        Action<SqlServerDbContextOptionsBuilder>? configureSqlServerDbContextOptionsBuilder)
        where TDbContext : DbContext
    {
        var sqlServerDbContextSparkOptions = serviceProvider
            .GetRequiredService<IOptionsMonitor<SqlServerDbContextSparkOptions<TDbContext>>>()
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


    private static void ConfigureObservability<TContext>(
        IHostApplicationBuilder builder,
        string serviceName,
        SqlServerDbContextSparkSettings<TContext> settings) where TContext : DbContext
    {
        if (settings.TracingEnabled)
            builder.Services.AddOpenTelemetry().WithTracing(tracerProviderBuilder =>
                tracerProviderBuilder.AddSqlClientInstrumentation());

        if (settings.HealthChecksEnabled)
            builder.Services.AddHealthChecks().AddDbContextCheck<TContext>(
                $"{DbContextSpark.Name}.{serviceName.Trim()}",
                tags: [DbContextSpark.Name]);
    }
}