using Microsoft.Data.SqlClient;

namespace ES.FX.Ignite.Microsoft.Data.SqlClient.Configuration;

/// <summary>
///     Provides the settings for connecting to a SQL Server database using a <see cref="SqlConnection" />
/// </summary>
public class SqlServerClientSparkSettings
{
    /// <summary>
    ///     Gets or sets a boolean value that indicates whether the database health check is disabled or not.
    /// </summary>
    public bool DisableHealthChecks { get; set; }

    /// <summary>
    ///     Gets or sets a boolean value that indicates whether the OpenTelemetry tracing is disabled or not.
    /// </summary>
    public bool DisableTracing { get; set; }
}