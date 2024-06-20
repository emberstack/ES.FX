using Microsoft.Data.SqlClient;

namespace ES.FX.Ignite.Microsoft.Data.SqlClient.Configuration;

/// <summary>
///     Provides the settings for connecting to a SQL Server database using a <see cref="SqlConnection" />
/// </summary>
public class SqlServerClientSparkSettings
{
    /// <summary>
    ///     Gets or sets a boolean value that indicates whether the database health checks are enabled.
    /// </summary>
    public bool HealthChecksEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a boolean value that indicates whether the OpenTelemetry tracing is enabled.
    /// </summary>
    public bool TracingEnabled { get; set; } = true;
}