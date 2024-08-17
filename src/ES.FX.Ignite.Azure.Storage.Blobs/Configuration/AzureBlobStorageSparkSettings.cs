using Azure.Storage.Blobs;
using ES.FX.Ignite.Spark.Configuration.Abstractions;

namespace ES.FX.Ignite.Azure.Storage.Blobs.Configuration;

/// <summary>
///     Provides the settings for connecting to Azure Storage using a <see cref="BlobServiceClient" />
/// </summary>
public class AzureBlobStorageSparkSettings
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