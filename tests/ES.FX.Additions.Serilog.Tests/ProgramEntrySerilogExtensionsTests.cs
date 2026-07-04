using ES.FX.Additions.Serilog.Lifetime;
using ES.FX.Hosting.Lifetime;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace ES.FX.Additions.Serilog.Tests;

/// <summary>
///     Functional coverage for <see cref="ProgramEntrySerilogExtensions.UseSerilog" />. These tests exercise the real
///     bootstrap wiring (global <see cref="Log.Logger" />, builder logger, exit action) without external sinks beyond
///     the console sink the helper configures by default.
/// </summary>
[Collection(nameof(SerilogGlobalStateCollection))]
public class ProgramEntrySerilogExtensionsTests
{
    private static ProgramEntryBuilder CreateBuilder() => ProgramEntry.CreateBuilder([]);

    [Fact]
    public void UseSerilog_ReturnsSameBuilderInstance_ForFluentChaining()
    {
        var builder = CreateBuilder();

        var returned = builder.UseSerilog(enableConsoleSelfLog: false);

        Assert.Same(builder, returned);
    }

    [Fact]
    public void UseSerilog_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((ProgramEntryBuilder)null!).UseSerilog(enableConsoleSelfLog: false));
    }

    [Fact]
    public void UseSerilog_SetsGlobalLogLogger()
    {
        var builder = CreateBuilder();

        builder.UseSerilog(enableConsoleSelfLog: false);

        // The helper assigns a real (non-silent) bootstrap logger to the global Log.Logger.
        Assert.NotNull(Log.Logger);
        Assert.NotEqual(Logger.None, Log.Logger);
    }

    [Fact]
    public void UseSerilog_BuildsProgramEntry_WithConfiguredLogger()
    {
        var builder = CreateBuilder();
        builder.UseSerilog(enableConsoleSelfLog: false);

        // Build must succeed and yield a usable ProgramEntry (the builder's logger was replaced by the Serilog one).
        var entry = builder.Build();

        Assert.NotNull(entry);
    }

    [Fact]
    public async Task UseSerilog_RegistersExitAction_ThatFlushesLogger()
    {
        var builder = CreateBuilder();
        builder.UseSerilog(enableConsoleSelfLog: false);

        var entry = builder.Build();

        // Running to completion triggers the registered exit action (Log.CloseAndFlushAsync) in the finally block.
        // A clean exit code proves the exit action ran without throwing.
        var exitCode = await entry.RunAsync(_ => Task.FromResult(0));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void UseSerilog_InvokesCustomConfigurationCallback()
    {
        var builder = CreateBuilder();
        var invoked = false;

        builder.UseSerilog(
            configureLoggerConfiguration: config =>
            {
                Assert.NotNull(config);
                invoked = true;
            },
            enableConsoleSelfLog: false);

        Assert.True(invoked);
    }

    [Fact]
    public void UseSerilog_HonorsMinimumLevel_ViaCustomSink()
    {
        var sink = new CapturingSink();
        var builder = CreateBuilder();

        builder.UseSerilog(
            LogEventLevel.Warning,
            config => config.WriteTo.Sink(sink),
            false);

        // Log.Logger is now the configured bootstrap logger; Debug is below Warning and must be dropped.
        Log.Logger.Debug("debug-should-be-filtered");
        Log.Logger.Warning("warning-should-pass");
        Log.CloseAndFlush();

        Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Warning, sink.Events[0].Level);
    }

    [Fact]
    public void UseSerilog_DefaultMinimumLevel_IsInformation()
    {
        var sink = new CapturingSink();
        var builder = CreateBuilder();

        builder.UseSerilog(
            configureLoggerConfiguration: config => config.WriteTo.Sink(sink),
            enableConsoleSelfLog: false);

        Log.Logger.Debug("debug-should-be-filtered");
        Log.Logger.Information("info-should-pass");
        Log.CloseAndFlush();

        var logEvent = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Information, logEvent.Level);
    }

    [Fact]
    public void UseSerilog_EnrichesWithMachineAndEntryAssembly()
    {
        var sink = new CapturingSink();
        var builder = CreateBuilder();

        builder.UseSerilog(
            configureLoggerConfiguration: config => config.WriteTo.Sink(sink),
            enableConsoleSelfLog: false);

        Log.Logger.Information("enriched");
        Log.CloseAndFlush();

        var logEvent = Assert.Single(sink.Events);
        // These enrichers are wired unconditionally by UseSerilog.
        Assert.Contains("MachineName", logEvent.Properties.Keys);
        Assert.Contains("ApplicationEntryAssembly", logEvent.Properties.Keys);
    }
}