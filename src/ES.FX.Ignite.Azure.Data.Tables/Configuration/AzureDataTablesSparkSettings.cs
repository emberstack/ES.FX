using Azure.Data.Tables;
using ES.FX.Ignite.Spark.Configuration.Abstractions;

namespace ES.FX.Ignite.Azure.Data.Tables.Configuration;

/// <summary>
///     Provides the settings for connecting to Azure Storage using a <see cref="TableServiceClient" />
/// </summary>
public class AzureDataTablesSparkSettings
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