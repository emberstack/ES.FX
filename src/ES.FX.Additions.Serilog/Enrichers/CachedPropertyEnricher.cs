using JetBrains.Annotations;
using Serilog.Core;
using Serilog.Events;

namespace ES.FX.Additions.Serilog.Enrichers;

/// <summary>
///     Base enricher that creates the <see cref="LogEventProperty" /> once and caches it for subsequent log events
/// </summary>
[PublicAPI]
public abstract class CachedPropertyEnricher : ILogEventEnricher
{
    private LogEventProperty? _cachedProperty;

    /// <summary>
    ///     Enriches the <paramref name="logEvent" /> with the cached property if it is not already present
    /// </summary>
    /// <param name="logEvent">The <see cref="LogEvent" /> to enrich</param>
    /// <param name="propertyFactory">The <see cref="ILogEventPropertyFactory" /> used to create the property</param>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(GetLogEventProperty(propertyFactory));
    }

    /// <summary>
    ///     Creates the <see cref="LogEventProperty" /> to cache
    /// </summary>
    /// <param name="propertyFactory">The <see cref="ILogEventPropertyFactory" /> used to create the property</param>
    /// <returns>The created <see cref="LogEventProperty" /></returns>
    protected abstract LogEventProperty CreateProperty(ILogEventPropertyFactory propertyFactory);

    private LogEventProperty GetLogEventProperty(ILogEventPropertyFactory propertyFactory)
    {
        return _cachedProperty ??= CreateProperty(propertyFactory);
    }
}