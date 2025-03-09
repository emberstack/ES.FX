using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.KubernetesClient;

/// <summary>
///     <see cref="KubernetesClientSpark" /> definition
/// </summary>
public static class KubernetesClientSpark
{
    /// <summary>
    ///     Spark name
    /// </summary>
    public const string Name = "KubernetesClient";

    /// <summary>
    ///     The default configuration section path
    /// </summary>
    public const string ConfigurationSectionPath = $"{IgniteConfigurationSections.Ignite}:{Name}";
}