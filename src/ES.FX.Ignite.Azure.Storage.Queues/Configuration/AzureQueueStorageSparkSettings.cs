using Azure.Storage.Queues;
using ES.FX.Ignite.Spark.Configuration.Abstractions;

namespace ES.FX.Ignite.Azure.Storage.Queues.Configuration;

/// <summary>
///     Provides the settings for connecting to Azure Storage using a <see cref="QueueServiceClient" />
/// </summary>
public class AzureQueueStorageSparkSettings
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