using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.Asp.Versioning;

/// <summary>
///     <see cref="ApiVersioningSpark" /> definition
/// </summary>
public static class ApiVersioningSpark
{
    /// <summary>
    ///     Spark name
    /// </summary>
    public const string Name = "ApiVersioning";

    /// <summary>
    ///     The default configuration section path
    /// </summary>
    public const string ConfigurationSectionPath = $"{IgniteConfigurationSections.Ignite}:{Name}";
}