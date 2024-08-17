using ES.FX.Ignite.Spark.Configuration.Abstractions;
using Microsoft.Data.SqlClient;

namespace ES.FX.Ignite.Microsoft.Data.SqlClient.Configuration;

/// <summary>
///     Provides the settings for connecting to a SQL Server database using a <see cref="SqlConnection" />
/// </summary>
public class SqlServerClientSparkSettings
{
    /// <summary>
    ///     <inheritdoc cref="SparkHealthCheckSettings" />
    /// </summary>
    public SparkHealthCheckSettings HealthChecks { get; set; } = new();


    /// <summary>
    ///     <inheritdoc cref="SparkTracingSettings" />
    /// </summary>
    public SparkTracingSettings Tracing { get; set; } = new();
}