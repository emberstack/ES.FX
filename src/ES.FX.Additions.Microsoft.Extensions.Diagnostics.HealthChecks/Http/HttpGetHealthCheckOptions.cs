namespace ES.FX.Additions.Microsoft.Extensions.Diagnostics.HealthChecks.Http;

/// <summary>
///     Options for the <see cref="HttpGetHealthCheck" />.
/// </summary>
public class HttpGetHealthCheckOptions
{
    /// <summary>
    ///     The URI to check.
    /// </summary>
    public required string Uri { get; set; }

    /// <summary>
    ///     An optional per-attempt timeout for the probe. When set, the request is cancelled after this duration and
    ///     the health check reports <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy" />.
    ///     When <see langword="null" />, no per-attempt timeout is applied and only the ambient cancellation token is honored.
    /// </summary>
    public TimeSpan? Timeout { get; set; }
}