using Azure.Data.Tables;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Configuration.OpenTelemetry;

namespace ES.FX.Ignite.Azure.Data.Tables.Configuration;

/// <summary>
///     Provides the settings for connecting to Azure Storage using a <see cref="TableServiceClient" />
/// </summary>
public class AzureDataTablesSparkSettings
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