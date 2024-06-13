using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.Spark;

/// <summary>
/// <see cref="DbContextSpark"/> definition
/// </summary>
public static class DbContextSpark
{
    /// <summary>
    /// Spark name
    /// </summary>
    public const string Name = "DbContext";

    /// <summary>
    /// The default configuration section key.
    /// </summary>
    public const string ConfigurationSectionKey = $"{IgniteConfigurationSections.Ignite}:{Name}";

}