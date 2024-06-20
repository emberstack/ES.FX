using Microsoft.EntityFrameworkCore;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Configuration;

/// <summary>
///     Provides the settings for connecting to a SQL Server database using EntityFrameworkCore using a
///     <see cref="TDbContext" />
/// </summary>
/// <typeparam name="TDbContext"><see cref="DbContext" /> type</typeparam>
public class SqlServerDbContextSparkSettings<TDbContext> where TDbContext : DbContext
{
    /// <summary>
    ///     Gets or sets a boolean value that indicates whether the database health checks are enabled or not.
    /// </summary>
    public bool HealthChecksEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a boolean value that indicates whether the OpenTelemetry tracing is enabled or not.
    /// </summary>
    public bool TracingEnabled { get; set; } = true;
}