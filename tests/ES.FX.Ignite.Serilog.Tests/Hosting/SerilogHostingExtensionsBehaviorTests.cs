using System.Collections.Concurrent;
using ES.FX.Additions.Serilog.Enrichers;
using ES.FX.Ignite.Serilog.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using ILogger = Serilog.ILogger;
using MelLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace ES.FX.Ignite.Serilog.Tests.Hosting;

public class SerilogHostingExtensionsBehaviorTests
{
    /// <summary>
    ///     A simple in-memory Serilog sink that records every <see cref="LogEvent" /> it receives.
    /// </summary>
    private sealed class CollectingSink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }

    /// <summary>
    ///     A Microsoft.Extensions.Logging provider that records the messages it receives so we can assert whether
    ///     Serilog forwarded events to MEL providers (the <c>writeToProviders</c> toggle).
    /// </summary>
    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) =>
            new RecordingLogger(Messages);

        public void Dispose()
        {
        }

        private sealed class RecordingLogger(ConcurrentQueue<string> messages) : Microsoft.Extensions.Logging.ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(MelLogLevel logLevel) => true;

            public void Log<TState>(MelLogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(formatter(state, exception));
        }
    }

    [Fact]
    public void ApplyDefaultConfiguration_True_RegistersApplicationNameEnricher()
    {
        var builder = WebApplication.CreateBuilder([]);

        builder.IgniteSerilog(applyDefaultConfiguration: true);

        var provider = builder.Build().Services;
        var enrichers = provider.GetServices<ILogEventEnricher>().ToArray();

        Assert.Contains(enrichers, e => e is ApplicationNameEnricher);
    }

    [Fact]
    public void ApplyDefaultConfiguration_False_DoesNotRegisterApplicationNameEnricher()
    {
        var builder = WebApplication.CreateBuilder([]);

        builder.IgniteSerilog(applyDefaultConfiguration: false);

        var provider = builder.Build().Services;
        var enrichers = provider.GetServices<ILogEventEnricher>().ToArray();

        Assert.DoesNotContain(enrichers, e => e is ApplicationNameEnricher);
    }

    [Fact]
    public void ApplyDefaultConfiguration_True_EmittedEventCarriesDefaultEnricherProperties()
    {
        var sink = new CollectingSink();
        var builder = WebApplication.CreateBuilder([]);

        builder.IgniteSerilog(cfg => cfg.WriteTo.Sink(sink), applyDefaultConfiguration: true);

        var provider = builder.Build().Services;
        var logger = provider.GetRequiredService<ILogger>();

        logger.Information("hello world");

        var evt = Assert.Single(sink.Events);
        // Default configuration wires FromLogContext + MachineName + EnvironmentName enrichers.
        Assert.True(evt.Properties.ContainsKey("MachineName"),
            "MachineName enricher (part of the default configuration) should have enriched the event.");
        Assert.True(evt.Properties.ContainsKey("EnvironmentName"),
            "EnvironmentName enricher (part of the default configuration) should have enriched the event.");
    }

    [Fact]
    public void ApplyDefaultConfiguration_False_DefaultEnrichersAreNotApplied()
    {
        var sink = new CollectingSink();
        var builder = WebApplication.CreateBuilder([]);

        builder.IgniteSerilog(cfg => cfg.WriteTo.Sink(sink), applyDefaultConfiguration: false);

        var provider = builder.Build().Services;
        var logger = provider.GetRequiredService<ILogger>();

        logger.Information("hello world");

        var evt = Assert.Single(sink.Events);
        Assert.False(evt.Properties.ContainsKey("MachineName"),
            "With applyDefaultConfiguration:false the default enrichers must not run.");
        Assert.False(evt.Properties.ContainsKey("EnvironmentName"),
            "With applyDefaultConfiguration:false the default enrichers must not run.");
    }

    [Fact]
    public void ApplyDefaultConfiguration_False_DefaultVerboseMinimumLevelIsNotForced()
    {
        // Default config forces MinimumLevel.Verbose(). Without it (and without any config override),
        // Serilog's default minimum level is Information, so a Verbose event must be dropped.
        var sink = new CollectingSink();
        var builder = WebApplication.CreateBuilder([]);

        builder.IgniteSerilog(cfg => cfg.WriteTo.Sink(sink), applyDefaultConfiguration: false);

        var provider = builder.Build().Services;
        var logger = provider.GetRequiredService<ILogger>();

        logger.Verbose("a verbose message");

        Assert.Empty(sink.Events);
    }

    [Fact]
    public void ApplyDefaultConfiguration_True_VerboseMinimumLevelIsApplied()
    {
        var sink = new CollectingSink();
        var builder = WebApplication.CreateBuilder([]);

        builder.IgniteSerilog(cfg => cfg.WriteTo.Sink(sink), applyDefaultConfiguration: true);

        var provider = builder.Build().Services;
        var logger = provider.GetRequiredService<ILogger>();

        logger.Verbose("a verbose message");

        // Default configuration set MinimumLevel.Verbose(), so the event must pass through.
        Assert.Single(sink.Events);
    }

    [Fact]
    public void ConfigureLoggerConfiguration_DelegateIsInvoked()
    {
        var invoked = false;
        var builder = WebApplication.CreateBuilder([]);

        builder.IgniteSerilog(_ => invoked = true);

        // AddSerilog builds the logger lazily; resolving it forces the configuration callback to run.
        _ = builder.Build().Services.GetRequiredService<ILogger>();

        Assert.True(invoked, "The configureLoggerConfiguration delegate should have been invoked.");
    }

    [Fact]
    public void ConfigureLoggerConfiguration_RunsAfterDefaultConfiguration()
    {
        // The XML-doc contract: "the default configuration will be applied before the custom configuration".
        // We prove ordering observably: the delegate lowers the minimum level to Fatal AFTER the default set it
        // to Verbose. If the delegate ran last, only Fatal survives. If defaults ran last, Information would pass.
        var sink = new CollectingSink();
        var builder = WebApplication.CreateBuilder([]);

        builder.IgniteSerilog(cfg =>
        {
            cfg.WriteTo.Sink(sink);
            cfg.MinimumLevel.Fatal();
        }, applyDefaultConfiguration: true);

        var provider = builder.Build().Services;
        var logger = provider.GetRequiredService<ILogger>();

        logger.Information("information should be filtered out");
        logger.Fatal("fatal should pass");

        var evt = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Fatal, evt.Level);
    }

    [Fact]
    public void WriteToProviders_True_ForwardsEventsToMelProviders()
    {
        var recordingProvider = new RecordingLoggerProvider();
        var builder = WebApplication.CreateBuilder([]);
        builder.Logging.AddProvider(recordingProvider);

        builder.IgniteSerilog(applyDefaultConfiguration: true, writeToProviders: true);

        var provider = builder.Build().Services;
        var melLogger = provider.GetRequiredService<ILogger<SerilogHostingExtensionsBehaviorTests>>();

        melLogger.LogInformation("forwarded to providers");

        Assert.Contains(recordingProvider.Messages, m => m.Contains("forwarded to providers"));
    }

    [Fact]
    public void WriteToProviders_False_DoesNotForwardEventsToMelProviders()
    {
        var recordingProvider = new RecordingLoggerProvider();
        var builder = WebApplication.CreateBuilder([]);
        builder.Logging.AddProvider(recordingProvider);

        builder.IgniteSerilog(applyDefaultConfiguration: true, writeToProviders: false);

        var provider = builder.Build().Services;
        var melLogger = provider.GetRequiredService<ILogger<SerilogHostingExtensionsBehaviorTests>>();

        melLogger.LogInformation("should not be forwarded");

        Assert.DoesNotContain(recordingProvider.Messages, m => m.Contains("should not be forwarded"));
    }

    [Fact]
    public void ReadFromConfiguration_MinimumLevelOverride_IsHonored()
    {
        var sink = new CollectingSink();
        var builder = WebApplication.CreateBuilder([]);

        // Force the base minimum level via the "Serilog" config section. With applyDefaultConfiguration:false
        // there is no code-forced level, so ReadFrom.Configuration is the sole authority.
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Serilog:MinimumLevel:Default"] = "Warning"
        });

        builder.IgniteSerilog(cfg => cfg.WriteTo.Sink(sink), applyDefaultConfiguration: false);

        var provider = builder.Build().Services;
        var logger = provider.GetRequiredService<ILogger>();

        logger.Information("below the configured warning threshold");
        logger.Warning("at the configured warning threshold");

        var evt = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Warning, evt.Level);
    }
}
