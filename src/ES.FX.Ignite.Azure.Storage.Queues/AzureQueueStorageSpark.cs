using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.Azure.Storage.Queues;

/// <summary>
///     <see cref="AzureQueueStorageSpark" /> definition
/// </summary>
public static class AzureQueueStorageSpark
{
    /// <summary>
    ///     Spark name
    /// </summary>
    public const string Name = "AzureQueueStorage";

    /// <summary>
    ///     The default configuration section path
    /// </summary>
    public const string ConfigurationSectionPath =
        $"{IgniteConfigurationSections.Ignite}:{nameof(Azure)}:{nameof(Storage)}:{nameof(Queues)}";
}