using ES.FX.Ignite.Spark.Configuration;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Configuration;

/// <summary>
///     Provides the settings for connecting to a SQL Server database using EntityFrameworkCore using a
///     <see cref="TDbContext" />
/// </summary>
/// <typeparam name="TDbContext"><see cref="DbContext" /> type</typeparam>
public class SqlServerDbContextSparkSettings<TDbContext>
    where TDbContext : DbContext
{
    /// <summary>
    ///     <inheritdoc cref="HealthCheckSettings" />
    /// </summary>
    public HealthCheckSettings HealthChecks { get; set; } = new();


    /// <summary>
    ///     <inheritdoc cref="TracingSettings" />
    /// </summary>
    public TracingSettings Tracing { get; set; } = new();
}