using ES.FX.Ignite.Spark;
using ES.FX.Serilog.Enrichers;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;

namespace ES.FX.Ignite.Serilog.Hosting;

[PublicAPI]
public static class SerilogHostingExtensions
{
    /// <summary>
    ///     Adds Serilog to the host builder with the default enrichers and default configuration
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// ///
    /// <param name="configureLoggerConfiguration">
    ///     An optional delegate that can be used for customizing the
    ///     <see cref="LoggerConfiguration" /> that will be used to construct a <see cref="Logger" />.
    /// </param>
    /// <param name="applyDefaultConfiguration">
    ///     Apply the default logger configuration. If enabled the default configuration
    ///     will be applied before the custom configuration.
    /// </param>
    /// <param name="writeToProviders">
    ///     By default, Serilog does not write events to <see cref="T:Microsoft.Extensions.Logging.ILoggerProvider" />s
    ///     registered through
    ///     the Microsoft.Extensions.Logging API. Normally, equivalent Serilog sinks are used in place of providers. Specify
    ///     <c>true</c> to write events to all providers.
    /// </param>
    public static void IgniteSerilog(this IHostApplicationBuilder builder,
        Action<LoggerConfiguration>? configureLoggerConfiguration = null, bool applyDefaultConfiguration = true,
        bool writeToProviders = true)
    {
        builder.GuardSparkConfiguration($"{nameof(Serilog)}",
            SparkGuard.AlreadyConfiguredError($"{nameof(Serilog)}"));

        builder.Services.AddSerilog((services, loggerConfiguration) =>
        {
            if (applyDefaultConfiguration)
                loggerConfiguration
                    .MinimumLevel.Verbose()
                    .Destructure.ToMaximumCollectionCount(64)
                    .Destructure.ToMaximumStringLength(2048)
                    .Destructure.ToMaximumDepth(16)
                    .Enrich.FromLogContext()
                    .Enrich.WithMachineName()
                    .Enrich.WithEnvironmentName()
                    .Enrich.With<EntryAssemblyNameEnricher>();

            loggerConfiguration
                .ReadFrom.Services(services)
                .ReadFrom.Configuration(builder.Configuration);

            configureLoggerConfiguration?.Invoke(loggerConfiguration);
        }, writeToProviders: writeToProviders);

        if (applyDefaultConfiguration) builder.Services.AddSingleton<ILogEventEnricher, ApplicationNameEnricher>();
    }
}