using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Ignite.Spark.Configuration;

public class HealthCheckSettings
{
    /// <summary>
    ///     Gets or sets a boolean value that indicates whether the health checks are enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Gets or sets the HealthStatus to use in case of HealthChecks failure. If not set, the default value is
    ///     <see cref="HealthStatus.Unhealthy" />
    /// </summary>
    public HealthStatus? FailureStatus { get; set; }

    /// <summary>
    ///     Gets or sets the timeout for the health checks.
    /// </summary>
    public TimeSpan? Timeout { get; set; }


    /// <summary>
    ///     Gets or sets the tags for the health checks.
    /// </summary>
    public string[] Tags { get; set; } = [];
}