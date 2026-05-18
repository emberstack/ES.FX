using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace ES.FX.Ignite.StackExchange.Redis.HealthChecks;

/// <summary>
///     Redis health check that pings the connection multiplexer.
/// </summary>
internal sealed class SimpleRedisHealthCheck(IConnectionMultiplexer connectionMultiplexer) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await connectionMultiplexer.GetDatabase().PingAsync().ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
