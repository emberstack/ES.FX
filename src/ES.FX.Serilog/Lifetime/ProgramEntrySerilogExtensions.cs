using ES.FX.Hosting.Lifetime;
using ES.FX.Serilog.Enrichers;
using ES.FX.Serilog.Sinks.Console;
using JetBrains.Annotations;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace ES.FX.Serilog.Lifetime;

[PublicAPI]
public static class ProgramEntrySerilogExtensions
{
    /// <summary>
    ///     Use Serilog as the logger for the ProgramEntry
    /// </summary>
    /// <param name="builder">The <see cref="ProgramEntryBuilder" /></param>
    /// <param name="minimumLevel">The minimum level for logging</param>
    /// <param name="configureLoggerConfiguration"> Action to configure the <see cref="LoggerConfiguration" />.</param>
    /// <param name="enableConsoleSelfLog">Enables the Serilog SelfLog to console (useful to debug Serilog)</param>
    /// <returns>The <see cref="ProgramEntryBuilder" /></returns>
    public static ProgramEntryBuilder UseSerilog(this ProgramEntryBuilder builder,
        LogEventLevel minimumLevel = LogEventLevel.Information,
        Action<LoggerConfiguration>? configureLoggerConfiguration = null, bool enableConsoleSelfLog = true)
    {
        // Enable Serilog SelfLog to console
        if (enableConsoleSelfLog) SelfLog.Enable(Console.Error);

        // Configure the logger configuration
        var loggerConfiguration = new LoggerConfiguration();

        loggerConfiguration
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.Console(outputTemplate: ConsoleOutputTemplates.Default)
            .Destructure.ToMaximumCollectionCount(64)
            .Destructure.ToMaximumStringLength(2048)
            .Destructure.ToMaximumDepth(16)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.With<EntryAssemblyNameEnricher>();

        configureLoggerConfiguration?.Invoke(loggerConfiguration);

        // Create a Serilog logger from the configuration
        // This logger will be replaced by the host logger during bootstrapping
        Log.Logger = loggerConfiguration.CreateBootstrapLogger();

        // Set the logger for the ProgramEntry to the logger created by Serilog
        builder.WithLogger(
            new SerilogLoggerFactory(Log.Logger)
                .CreateLogger(typeof(ProgramEntry).FullName ?? nameof(ProgramEntry)));

        // Handle application exit by closing and flushing the Serilog logger
        builder.AddExitAction(async _ => await Log.CloseAndFlushAsync());

        return builder;
    }
}