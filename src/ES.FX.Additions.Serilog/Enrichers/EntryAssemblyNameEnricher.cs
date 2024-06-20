using System.Reflection;
using JetBrains.Annotations;
using Serilog.Core;
using Serilog.Events;

namespace ES.FX.Additions.Serilog.Enrichers;

/// <summary>
///     Enricher for setting the application entry assembly on the ApplicationEntryAssembly property
/// </summary>
[PublicAPI]
public class EntryAssemblyNameEnricher : CachedPropertyEnricher
{
    protected override LogEventProperty CreateProperty(ILogEventPropertyFactory propertyFactory) =>
        propertyFactory.CreateProperty("ApplicationEntryAssembly", Assembly.GetEntryAssembly()?.FullName);
}