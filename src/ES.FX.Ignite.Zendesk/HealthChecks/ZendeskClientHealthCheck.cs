using ES.FX.Zendesk.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Ignite.Zendesk.HealthChecks;

/// <summary>
///     Verifies that Zendesk is reachable and the configured credentials are valid by calling
///     <c>GET /api/v2/users/me.json</c>.
/// </summary>
internal sealed class ZendeskClientHealthCheck(IZendeskClient client) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await client.Users.GetCurrentUserAsync(cancellationToken).ConfigureAwait(false);
            // Avoid PII (email) in the health description — it can surface on an unauthenticated /health endpoint.
            return HealthCheckResult.Healthy($"Authenticated with Zendesk as user {user.Id} ({user.Role}).");
        }
        catch (Exception exception)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Failed to reach Zendesk.", exception);
        }
    }
}