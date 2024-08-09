using Azure.Security.KeyVault.Secrets;
using ES.FX.Ignite.Spark.Configuration.Abstractions;

namespace ES.FX.Ignite.Azure.Security.KeyVault.Secrets.Configuration;

/// <summary>
///     Provides the settings for connecting to Azure KeyVault using a <see cref="SecretClient" />
/// </summary>
public class AzureKeyVaultSecretsSparkSettings : ISparkHealthCheckSettings, ISparkTracingSettings
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