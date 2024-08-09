using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.Azure.Storage.Blobs;

/// <summary>
///     <see cref="AzureBlobStorageSpark" /> definition
/// </summary>
public static class AzureBlobStorageSpark
{
    /// <summary>
    ///     Spark name
    /// </summary>
    public const string Name = "AzureBlobStorage";

    /// <summary>
    ///     The default configuration section path
    /// </summary>
    public const string ConfigurationSectionPath =
        $"{IgniteConfigurationSections.Ignite}:{nameof(Azure)}:{nameof(Storage)}:{nameof(Blobs)}";
}