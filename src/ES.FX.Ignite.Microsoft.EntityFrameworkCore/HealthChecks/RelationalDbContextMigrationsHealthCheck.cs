using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.HealthChecks;

/// <summary>
///     Health check that reports pending relational migrations for <typeparamref name="TContext" />.
/// </summary>
/// <typeparam name="TContext">The <see cref="DbContext" /> type to check for pending migrations.</typeparam>
/// <param name="dbContext">The <typeparamref name="TContext" /> instance used to query pending migrations.</param>
public class RelationalDbContextMigrationsHealthCheck<TContext>(TContext dbContext) : IHealthCheck
    where TContext : DbContext
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        // Registration is unset when the check is invoked directly (outside the health check service)
        var failureStatus = context.Registration?.FailureStatus ?? HealthStatus.Unhealthy;
        try
        {
            var migrations =
                (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToArray();
            return migrations.Length == 0
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(failureStatus,
                    $"Database has {migrations.Length} pending migration(s)");
        }
        catch (Exception exception)
        {
            return new HealthCheckResult(failureStatus, exception: exception);
        }
    }
}