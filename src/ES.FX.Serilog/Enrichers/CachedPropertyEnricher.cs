using Serilog.Core;
using Serilog.Events;

namespace ES.FX.Serilog.Enrichers
{
    public abstract class CachedPropertyEnricher : ILogEventEnricher
    {
        private LogEventProperty? _cachedProperty;

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddPropertyIfAbsent(this.GetLogEventProperty(propertyFactory));
        }

        private LogEventProperty GetLogEventProperty(ILogEventPropertyFactory propertyFactory)
        {
            return _cachedProperty ??= CreateProperty(propertyFactory);
        }

        protected abstract LogEventProperty CreateProperty(ILogEventPropertyFactory propertyFactory);
    }
}
