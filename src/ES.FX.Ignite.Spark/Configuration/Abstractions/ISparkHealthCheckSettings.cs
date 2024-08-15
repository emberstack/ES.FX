using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Ignite.Spark.Configuration.Abstractions;

public interface ISparkHealthCheckSettings
{
    /// <summary>
    ///     Gets or sets a boolean value that indicates whether the health checks are enabled.
    /// </summary>
    public bool HealthChecksEnabled { get; set; }

    /// <summary>
    ///     Gets or sets the HealthStatus to use in case of HealthChecks failure. If not set, the default value is
    ///     <see cref="HealthStatus.Unhealthy" />
    /// </summary>
    public HealthStatus? HealthChecksFailureStatus { get; set; }
}