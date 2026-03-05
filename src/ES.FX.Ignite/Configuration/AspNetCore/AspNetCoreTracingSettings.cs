using ES.FX.Ignite.Spark.Configuration.OpenTelemetry;

namespace ES.FX.Ignite.Configuration.AspNetCore;

public class AspNetCoreTracingSettings : TracingSettings
{
    /// <summary>
    ///     Gets or sets a value indicating whether ASP.NET Core tracing will filter out health checks requests
    /// </summary>
    public bool HealthChecksFiltered { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the client address (client.address) tag should be set
    ///     from the request's RemoteIpAddress. This ensures the correct client IP is reported when
    ///     forwarded headers are used.
    /// </summary>
    public bool EnrichClientAddressFromRemoteIpAddress { get; set; } = true;
}