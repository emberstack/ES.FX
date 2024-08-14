using Azure.Data.Tables;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Ignite.Azure.Data.Tables.HealthChecks;

/// <summary>
///     Azure Tables health check.
/// </summary>
internal sealed class SimpleTableServiceHealthCheck(
    TableServiceClient tableServiceClient,
    bool useFilter)
    : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await tableServiceClient
                .QueryAsync(useFilter ? "false" : null, cancellationToken: cancellationToken)
                .GetAsyncEnumerator(cancellationToken)
                .MoveNextAsync()
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}