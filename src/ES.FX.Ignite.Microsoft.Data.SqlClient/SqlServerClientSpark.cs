using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.Microsoft.Data.SqlClient;

/// <summary>
///     <see cref="SqlServerClientSpark" /> definition
/// </summary>
public static class SqlServerClientSpark
{
    /// <summary>
    ///     Spark name
    /// </summary>
    public const string Name = "SqlServerClient";

    /// <summary>
    ///     The default configuration section path
    /// </summary>
    public const string ConfigurationSectionPath = $"{IgniteConfigurationSections.Ignite}:{Name}";
}