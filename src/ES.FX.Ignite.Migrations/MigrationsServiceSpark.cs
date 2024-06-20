using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.Migrations;

/// <summary>
///     <see cref="MigrationsServiceSpark" /> definition
/// </summary>
public static class MigrationsServiceSpark
{
    /// <summary>
    ///     Spark name
    /// </summary>
    public const string Name = "MigrationsService";

    /// <summary>
    ///     The default configuration section path
    /// </summary>
    public const string ConfigurationSectionPath = $"{IgniteConfigurationSections.Services}:{Name}";
}