using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
///     A diagnostic health check implementation using HTTP status codes
/// </summary>
public class HttpGetHealthCheck(HttpGetHealthCheckOptions options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext _,
        CancellationToken cancellationToken = default)
    {
        var client = new HttpClient(new SocketsHttpHandler { ActivityHeadersPropagator = null });
        using var response = await client.GetAsync(options.Uri, cancellationToken)
            .ConfigureAwait(false);

        return response.IsSuccessStatusCode
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
    }
}