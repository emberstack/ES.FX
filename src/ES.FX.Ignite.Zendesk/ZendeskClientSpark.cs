using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.Zendesk;

/// <summary>
///     <see cref="ZendeskClientSpark" /> definition. Configures the <c>ES.FX.Zendesk</c> typed client
///     into Ignite (config binding, DI, health check, OpenTelemetry).
/// </summary>
public static class ZendeskClientSpark
{
    /// <summary>
    ///     Spark name
    /// </summary>
    public const string Name = "ZendeskClient";

    /// <summary>
    ///     The default configuration section path (<c>Ignite:Zendesk</c>).
    /// </summary>
    public const string ConfigurationSectionPath = $"{IgniteConfigurationSections.Ignite}:Zendesk";
}