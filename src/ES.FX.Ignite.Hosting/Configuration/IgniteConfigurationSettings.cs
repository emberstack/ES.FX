namespace ES.FX.Ignite.Hosting.Configuration;

/// <summary>
///     Settings for Configuration
/// </summary>
public class IgniteConfigurationSettings
{
    /// <summary>
    ///     Additional configuration files to load. All files will be optional and will reload configuration on change
    /// </summary>
    public string[] AdditionalJsonSettingsFiles { get; set; } = [];

    /// <summary>
    ///     Additional appsettings.{Environment}.{override}.json files. All files will be optional and will reload
    ///     configuration on change
    /// </summary>
    public string[] AdditionalJsonAppSettingsOverrides { get; set; } = [];
}