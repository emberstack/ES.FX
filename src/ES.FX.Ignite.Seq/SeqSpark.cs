using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.Seq;

/// <summary>
///     <see cref="SeqSpark" /> definition
/// </summary>
public static class SeqSpark
{
    /// <summary>
    ///     Spark name
    /// </summary>
    public const string Name = "Seq";

    /// <summary>
    ///     The default configuration section path
    /// </summary>
    public const string ConfigurationSectionPath = $"{IgniteConfigurationSections.Ignite}:{Name}";
}