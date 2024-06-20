using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.Azure.Data.Tables;

/// <summary>
///     <see cref="AzureDataTablesSpark" /> definition
/// </summary>
public static class AzureDataTablesSpark
{
    /// <summary>
    ///     Spark name
    /// </summary>
    public const string Name = "AzureDataTables";

    /// <summary>
    ///     The default configuration section path
    /// </summary>
    public const string ConfigurationSectionPath =
        $"{IgniteConfigurationSections.Ignite}:{nameof(Azure)}:{nameof(Data)}:{nameof(Tables)}";
}