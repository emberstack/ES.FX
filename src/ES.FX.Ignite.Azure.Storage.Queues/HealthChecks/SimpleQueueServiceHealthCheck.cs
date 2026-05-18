using Azure.Storage.Queues;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Ignite.Azure.Storage.Queues.HealthChecks;

/// <summary>
///     Azure Queue Storage health check.
/// </summary>
internal sealed class SimpleQueueServiceHealthCheck(QueueServiceClient queueServiceClient) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await queueServiceClient.GetPropertiesAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
