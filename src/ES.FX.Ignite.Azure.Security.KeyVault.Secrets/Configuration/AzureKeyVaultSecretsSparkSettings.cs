using Azure.Security.KeyVault.Secrets;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Configuration.OpenTelemetry;

namespace ES.FX.Ignite.Azure.Security.KeyVault.Secrets.Configuration;

/// <summary>
///     Provides the settings for connecting to Azure KeyVault using a <see cref="SecretClient" />
/// </summary>
public class AzureKeyVaultSecretsSparkSettings
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