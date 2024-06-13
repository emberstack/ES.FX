using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;
using Serilog.Core.Enrichers;

namespace ES.FX.Serilog.Enrichers;

/// <summary>
///     Enricher for setting the application name on the property ApplicationName
/// </summary>
[PublicAPI]
public class ApplicationNameEnricher(IHostEnvironment hostEnvironment)
    : PropertyEnricher(nameof(IHostEnvironment.ApplicationName), hostEnvironment.ApplicationName)
{
}