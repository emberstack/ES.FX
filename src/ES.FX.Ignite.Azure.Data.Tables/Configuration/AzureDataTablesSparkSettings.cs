using Azure.Data.Tables;
using ES.FX.Ignite.Spark.Configuration.Abstractions;

namespace ES.FX.Ignite.Azure.Data.Tables.Configuration;

/// <summary>
///     Provides the settings for connecting to Azure Storage using a <see cref="TableServiceClient" />
/// </summary>
public class AzureDataTablesSparkSettings : ISparkHealthCheckSettings, ISparkTracingSettings
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