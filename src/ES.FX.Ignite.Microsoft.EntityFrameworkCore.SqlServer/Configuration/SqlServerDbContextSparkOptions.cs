using Microsoft.EntityFrameworkCore;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Configuration;

/// <summary>
///     Provides the options for connecting to a SQL Server database using EntityFrameworkCore using the
///     <see cref="TDbContext" />
/// </summary>
/// <typeparam name="TDbContext"><see cref="DbContext" /> type</typeparam>
public class SqlServerDbContextSparkOptions<TDbContext> where TDbContext : DbContext
{
    /// <summary>
    ///     The connection string of the SQL server database to connect to.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    ///     Gets or sets whether retries should be disabled.
    /// </summary>
    public bool DisableRetry { get; set; }

    /// <summary>
    ///     Gets or sets the time in seconds to wait for the command to execute.
    /// </summary>
    public int? CommandTimeout { get; set; }
}