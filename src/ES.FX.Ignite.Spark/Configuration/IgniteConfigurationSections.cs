namespace ES.FX.Ignite.Spark.Configuration;

/// <summary>
///     Ignite default configuration sections
/// </summary>
public static class IgniteConfigurationSections
{
    public const string Ignite = nameof(FX.Ignite);
    public const string Services = $"{Ignite}:{nameof(Services)}";
}