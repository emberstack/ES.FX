using ES.FX.Ignite.Spark.Configuration.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Configuration;

/// <summary>
///     Provides the settings for connecting to a SQL Server database using EntityFrameworkCore using a
///     <see cref="TDbContext" />
/// </summary>
/// <typeparam name="TDbContext"><see cref="DbContext" /> type</typeparam>
public class SqlServerDbContextSparkSettings<TDbContext> : ISparkHealthCheckSettings, ISparkTracingSettings
    where TDbContext : DbContext
{
    /// <summary>
    ///     <inheritdoc cref="ISparkHealthCheckSettings.HealthChecksEnabled" />
    /// </summary>
    public bool HealthChecksEnabled { get; set; } = true;

    /// <summary>
    ///     <inheritdoc cref="ISparkTracingSettings.TracingEnabled" />
    /// </summary>
    public bool TracingEnabled { get; set; } = true;
}