using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.NousResearch.HermesAgent;

/// <summary>
///     <see cref="HermesAgentClientSpark" /> definition. Configures the <c>ES.FX.NousResearch.HermesAgent</c>
///     typed client into Ignite (config binding, DI, health check, OpenTelemetry).
/// </summary>
public static class HermesAgentClientSpark
{
    /// <summary>
    ///     Spark name
    /// </summary>
    public const string Name = "HermesAgentClient";

    /// <summary>
    ///     The default configuration section path (<c>Ignite:NousResearch:HermesAgent</c>).
    /// </summary>
    public const string ConfigurationSectionPath = $"{IgniteConfigurationSections.Ignite}:NousResearch:HermesAgent";
}