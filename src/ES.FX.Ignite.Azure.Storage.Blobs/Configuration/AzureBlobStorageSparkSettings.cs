using Azure.Storage.Blobs;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Configuration.OpenTelemetry;

namespace ES.FX.Ignite.Azure.Storage.Blobs.Configuration;

/// <summary>
///     Provides the settings for connecting to Azure Storage using a <see cref="BlobServiceClient" />
/// </summary>
public class AzureBlobStorageSparkSettings
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