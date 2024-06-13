using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.Migrations.Spark;

/// <summary>
/// <see cref="MigrationsServiceSpark"/> definition
/// </summary>
public static class MigrationsServiceSpark
{
    /// <summary>
    /// Spark name
    /// </summary>
    public const string Name = "MigrationsService";

    /// <summary>
    /// The default configuration section key.
    /// </summary>
    public const string ConfigurationSectionKey = $"{IgniteConfigurationSections.Services}:{Name}";

}