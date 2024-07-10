using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.FluentValidation;

/// <summary>
///     <see cref="FluentValidationSpark" /> definition
/// </summary>
public static class FluentValidationSpark
{
    /// <summary>
    ///     Spark name
    /// </summary>
    public const string Name = "FluentValidation";

    /// <summary>
    ///     The default configuration section path
    /// </summary>
    public const string ConfigurationSectionPath = $"{IgniteConfigurationSections.Ignite}:{Name}";
}