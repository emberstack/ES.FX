using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace ES.FX.Ignite.StackExchange.Redis.HealthChecks;

/// <summary>
///     Redis health check.
/// </summary>
/// <remarks>
///     Iterates every configured endpoint on the multiplexer. For non-cluster servers, both the database
///     and the server endpoint are pinged. For cluster nodes, <c>CLUSTER INFO</c> is executed and the
///     response is inspected for <c>cluster_state:ok</c>.
/// </remarks>
internal sealed class SimpleRedisHealthCheck(IConnectionMultiplexer connectionMultiplexer) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var endPoint in connectionMultiplexer.GetEndPoints(true))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var server = connectionMultiplexer.GetServer(endPoint);

                if (server.ServerType != ServerType.Cluster)
                {
                    await connectionMultiplexer.GetDatabase().PingAsync().WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                    await server.PingAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var clusterInfo = await server.ExecuteAsync("CLUSTER", "INFO").WaitAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (clusterInfo.IsNull)
                    return new HealthCheckResult(context.Registration.FailureStatus,
                        $"INFO CLUSTER is null or can't be read for endpoint {endPoint}");

                if (!clusterInfo.ToString()!.Contains("cluster_state:ok"))
                    return new HealthCheckResult(context.Registration.FailureStatus,
                        $"INFO CLUSTER is not on OK state for endpoint {endPoint}");
            }

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}