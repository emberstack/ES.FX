using ES.FX.Ignite.Spark.Configuration.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Ignite.Microsoft.Data.SqlClient.Configuration;

/// <summary>
///     Provides the settings for connecting to a SQL Server database using a <see cref="SqlConnection" />
/// </summary>
public class SqlServerClientSparkSettings : ISparkHealthCheckSettings, ISparkTracingSettings
{
    /// <summary>
    ///     <inheritdoc cref="ISparkHealthCheckSettings.HealthChecksEnabled" />
    /// </summary>
    public bool HealthChecksEnabled { get; set; } = true;

    /// <summary>
    ///     <inheritdoc cref="ISparkHealthCheckSettings.HealthChecksFailureStatus" />
    /// </summary>
    public HealthStatus? HealthChecksFailureStatus { get; set; }

    /// <summary>
    ///     <inheritdoc cref="ISparkTracingSettings.TracingEnabled" />
    /// </summary>
    public bool TracingEnabled { get; set; } = true;
}