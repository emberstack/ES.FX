using Azure.Storage.Queues;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Ignite.Azure.Storage.Queues.HealthChecks;

/// <summary>
///     Azure Queue Storage health check.
/// </summary>
/// <remarks>
///     Uses a page-list probe (page size 1) instead of <c>QueueServiceClient.GetPropertiesAsync</c> so the check
///     works with the least-privileged role assignment ("Storage Queue Data Reader" at storage account level).
///     <c>GetPropertiesAsync</c> requires elevated permissions that "Storage Queue Data Contributor" does not grant.
/// </remarks>
internal sealed class SimpleQueueServiceHealthCheck(QueueServiceClient queueServiceClient) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await foreach (var _ in queueServiceClient
                               .GetQueuesAsync(cancellationToken: cancellationToken)
                               .AsPages(pageSizeHint: 1)
                               .WithCancellation(cancellationToken)
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