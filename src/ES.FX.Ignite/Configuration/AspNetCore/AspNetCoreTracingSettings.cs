using ES.FX.Ignite.Spark.Configuration.OpenTelemetry;

namespace ES.FX.Ignite.Configuration.AspNetCore;

public class AspNetCoreTracingSettings : TracingSettings
{
    /// <summary>
    ///     Gets or sets a value indicating whether ASP.NET Core tracing will filter out health checks requests
    /// </summary>
    public bool HealthChecksFiltered { get; set; } = true;
}