using Azure.Security.KeyVault.Secrets;
using ES.FX.Ignite.Spark.Configuration.Abstractions;

namespace ES.FX.Ignite.Azure.Security.KeyVault.Secrets.Configuration;

/// <summary>
///     Provides the settings for connecting to Azure KeyVault using a <see cref="SecretClient" />
/// </summary>
public class AzureKeyVaultSecretsSparkSettings
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