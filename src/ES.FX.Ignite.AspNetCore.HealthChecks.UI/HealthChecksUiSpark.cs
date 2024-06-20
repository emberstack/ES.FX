using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.AspNetCore.HealthChecks.UI;

public static class HealthChecksUiSpark
{
    /// <summary>
    ///     Spark name
    /// </summary>
    public const string Name = "HealthChecksUi";

    /// <summary>
    ///     The default configuration section path
    /// </summary>
    public const string ConfigurationSectionPath = $"{IgniteConfigurationSections.Ignite}:{Name}";
}