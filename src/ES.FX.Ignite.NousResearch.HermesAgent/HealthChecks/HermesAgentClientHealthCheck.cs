using ES.FX.NousResearch.HermesAgent.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Ignite.NousResearch.HermesAgent.HealthChecks;

/// <summary>
///     Verifies that the Hermes Agent API server is reachable and the configured API key is valid by calling
///     <c>GET /v1/capabilities</c> (an authenticated endpoint).
/// </summary>
internal sealed class HermesAgentClientHealthCheck(IHermesAgentClient client) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var capabilities = await client.Server.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
            // Only non-sensitive identity data (platform + model) — the description can surface on an
            // unauthenticated /health endpoint.
            return HealthCheckResult.Healthy(
                $"Authenticated with Hermes Agent server (platform {capabilities.Platform}, model {capabilities.Model}).");
        }
        catch (Exception exception)
        {
            return new HealthCheckResult(context.Registration.FailureStatus,
                "Failed to reach the Hermes Agent server.", exception);
        }
    }
}