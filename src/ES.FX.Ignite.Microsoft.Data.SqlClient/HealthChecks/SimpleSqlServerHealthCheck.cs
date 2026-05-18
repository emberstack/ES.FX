using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Ignite.Microsoft.Data.SqlClient.HealthChecks;

/// <summary>
///     SQL Server health check that opens a connection and executes a probe query.
/// </summary>
internal sealed class SimpleSqlServerHealthCheck(string connectionString) : IHealthCheck
{
    private const string HealthQuery = "SELECT 1;";

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var command = connection.CreateCommand();
            command.CommandText = HealthQuery;
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
