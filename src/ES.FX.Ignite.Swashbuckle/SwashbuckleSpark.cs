using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.Swashbuckle;

/// <summary>
///     <see cref="SwashbuckleSpark" /> definition
/// </summary>
public static class SwashbuckleSpark
{
    /// <summary>
    ///     Spark name
    /// </summary>
    public const string Name = "Swashbuckle";

    /// <summary>
    ///     The default configuration section path
    /// </summary>
    public const string ConfigurationSectionPath = $"{IgniteConfigurationSections.Ignite}:{Name}";
}