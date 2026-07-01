using Azure.Data.Tables;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Ignite.Azure.Data.Tables.HealthChecks;

/// <summary>
///     Azure Tables health check.
/// </summary>
internal sealed class SimpleTableServiceHealthCheck(
    TableServiceClient tableServiceClient)
    : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await foreach (var _ in tableServiceClient
                               .QueryAsync((string?)null, 1,
                                   cancellationToken)
                               .ConfigureAwait(false))
                break;

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}