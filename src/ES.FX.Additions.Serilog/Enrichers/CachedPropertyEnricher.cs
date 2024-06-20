using Serilog.Core;
using Serilog.Events;

namespace ES.FX.Additions.Serilog.Enrichers;

public abstract class CachedPropertyEnricher : ILogEventEnricher
{
    private LogEventProperty? _cachedProperty;

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(GetLogEventProperty(propertyFactory));
    }

    protected abstract LogEventProperty CreateProperty(ILogEventPropertyFactory propertyFactory);

    private LogEventProperty GetLogEventProperty(ILogEventPropertyFactory propertyFactory)
    {
        return _cachedProperty ??= CreateProperty(propertyFactory);
    }
}