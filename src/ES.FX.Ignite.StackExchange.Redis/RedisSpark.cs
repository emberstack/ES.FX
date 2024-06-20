using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.StackExchange.Redis;

/// <summary>
///     <see cref="RedisSpark" /> definition
/// </summary>
public static class RedisSpark
{
    /// <summary>
    ///     Spark name
    /// </summary>
    public const string Name = "Redis";

    /// <summary>
    ///     The default configuration section path
    /// </summary>
    public const string ConfigurationSectionPath = $"{IgniteConfigurationSections.Ignite}:{Name}";
}