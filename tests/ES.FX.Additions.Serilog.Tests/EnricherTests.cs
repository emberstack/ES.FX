using System.Reflection;
using ES.FX.Additions.Serilog.Enrichers;
using Microsoft.Extensions.Hosting;
using Moq;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace ES.FX.Additions.Serilog.Tests;

/// <summary>
///     Functional coverage for the Serilog enrichers. Each test drives a real Serilog pipeline through a
///     <see cref="CapturingSink" /> and asserts on the emitted <see cref="LogEvent" /> properties.
/// </summary>
public class EnricherTests
{
    private static (ILogger logger, CapturingSink sink) CreateLogger(ILogEventEnricher enricher)
    {
        var sink = new CapturingSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.With(enricher)
            .WriteTo.Sink(sink)
            .CreateLogger();
        return (logger, sink);
    }

    [Fact]
    public void ApplicationNameEnricher_AddsApplicationNameProperty()
    {
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(e => e.ApplicationName).Returns("MyTestApp");

        var (logger, sink) = CreateLogger(new ApplicationNameEnricher(hostEnvironment.Object));
        logger.Information("hello");

        var logEvent = Assert.Single(sink.Events);
        Assert.True(logEvent.Properties.TryGetValue(nameof(IHostEnvironment.ApplicationName), out var value));
        Assert.Equal("\"MyTestApp\"", value!.ToString());
    }

    [Fact]
    public void ApplicationNameEnricher_PropertyNameIsApplicationName()
    {
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(e => e.ApplicationName).Returns("Whatever");

        var (logger, sink) = CreateLogger(new ApplicationNameEnricher(hostEnvironment.Object));
        logger.Information("hello");

        var logEvent = Assert.Single(sink.Events);
        Assert.Contains("ApplicationName", logEvent.Properties.Keys);
    }

    [Fact]
    public void EntryAssemblyNameEnricher_AddsApplicationEntryAssemblyProperty()
    {
        var (logger, sink) = CreateLogger(new EntryAssemblyNameEnricher());
        logger.Information("hello");

        var logEvent = Assert.Single(sink.Events);
        // The entry assembly under the test runner may be null; the property is still added (with a null/scalar value).
        Assert.Contains("ApplicationEntryAssembly", logEvent.Properties.Keys);
    }

    [Fact]
    public void EntryAssemblyNameEnricher_ValueMatchesEntryAssemblyFullName()
    {
        var (logger, sink) = CreateLogger(new EntryAssemblyNameEnricher());
        logger.Information("hello");

        var logEvent = Assert.Single(sink.Events);
        var property = Assert.IsType<ScalarValue>(logEvent.Properties["ApplicationEntryAssembly"]);

        var expected = Assembly.GetEntryAssembly()?.FullName;
        Assert.Equal(expected, property.Value);
    }

    [Fact]
    public void EntryAssemblyNameEnricher_DoesNotOverrideExistingProperty()
    {
        // AddPropertyIfAbsent semantics: an enricher earlier in the chain should win.
        var sink = new CapturingSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.WithProperty("ApplicationEntryAssembly", "PreExisting")
            .Enrich.With(new EntryAssemblyNameEnricher())
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("hello");

        var logEvent = Assert.Single(sink.Events);
        var property = Assert.IsType<ScalarValue>(logEvent.Properties["ApplicationEntryAssembly"]);
        Assert.Equal("PreExisting", property.Value);
    }

    [Fact]
    public void CachedPropertyEnricher_CreatesPropertyOnlyOnce()
    {
        var enricher = new CountingEnricher();
        var (logger, sink) = CreateLogger(enricher);

        logger.Information("one");
        logger.Information("two");
        logger.Information("three");

        Assert.Equal(3, sink.Events.Count);
        // The cached base should only invoke CreateProperty a single time despite three log events.
        Assert.Equal(1, enricher.CreateCount);
        Assert.All(sink.Events, e => Assert.Contains("Counted", e.Properties.Keys));
    }

    private sealed class CountingEnricher : CachedPropertyEnricher
    {
        private int _createCount;
        public int CreateCount => _createCount;

        protected override LogEventProperty CreateProperty(ILogEventPropertyFactory propertyFactory)
        {
            Interlocked.Increment(ref _createCount);
            return propertyFactory.CreateProperty("Counted", "value");
        }
    }
}
