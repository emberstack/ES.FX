using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.Azure.Security.KeyVault.Secrets;

/// <summary>
///     <see cref="AzureKeyVaultSecretsSpark" /> definition
/// </summary>
public static class AzureKeyVaultSecretsSpark
{
    /// <summary>
    ///     Spark name
    /// </summary>
    public const string Name = "AzureKeyVaultSecrets";

    /// <summary>
    ///     The default configuration section path
    /// </summary>
    public const string ConfigurationSectionPath =
        $"{IgniteConfigurationSections.Ignite}:{nameof(Azure)}:{nameof(KeyVault)}:{nameof(Secrets)}";
}