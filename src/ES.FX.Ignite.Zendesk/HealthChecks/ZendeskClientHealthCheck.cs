using ES.FX.Zendesk.Support;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Ignite.Zendesk.HealthChecks;

/// <summary>
///     Verifies that Zendesk is reachable and the configured credentials are valid by calling
///     <c>GET /api/v2/users/me</c>.
/// </summary>
internal sealed class ZendeskClientHealthCheck(ZendeskSupportApiClient client) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await client.Api.V2.Users.Me.GetAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var user = response?.User ??
                       throw new InvalidOperationException(
                           "Zendesk returned an empty response for the current user.");
            // Avoid PII (email) in the health description — it can surface on an unauthenticated /health endpoint.
            return HealthCheckResult.Healthy($"Authenticated with Zendesk as user {user.Id} ({user.Role}).");
        }
        catch (Exception exception)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Failed to reach Zendesk.", exception);
        }
    }
}