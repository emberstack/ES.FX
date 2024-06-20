using ES.FX.Ignite.Spark.Configuration.OpenTelemetry;

namespace ES.FX.Ignite.Configuration.AspNetCore;

public class AspNetCoreMetricsSettings : MetricsSettings
{
    /// <summary>
    ///     Gets or sets a value indicating whether ASP.NET Core metrics will filter out health checks requests
    /// </summary>
    public bool HealthChecksFiltered { get; set; } = true;
}