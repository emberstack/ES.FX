using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Ignite.Configuration.AspNetCore;

/// <summary>
///     Settings for HealthChecks
/// </summary>
public class AspNetCoreHealthChecksSettings
{
    /// <summary>
    ///     Gets or sets a value indicating whether the health checks are enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the readiness endpoint path
    /// </summary>
    public string ReadinessEndpointPath { get; set; } = "/health/ready";

    /// <summary>
    ///     Gets or sets the liveness endpoint path
    /// </summary>
    public string LivenessEndpointPath { get; set; } = "/health/live";

    /// <summary>
    ///     Gets or sets a value indicating whether the application status check is enabled
    /// </summary>
    public bool ApplicationStatusCheckEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the response writer for the health checks endpoints
    /// </summary>
    public Func<HttpContext, HealthReport, Task>? ResponseWriter { get; set; }
}