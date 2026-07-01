using Microsoft.Extensions.Configuration;

namespace ES.FX.Ignite.Spark.Configuration;

public static class SparkConfig
{
    /// <summary>
    ///     Default configuration section for settings
    /// </summary>
    public const string Settings = "Settings";

    /// <summary>
    ///     Gets the settings from the configuration.
    /// </summary>
    /// <typeparam name="T">Settings type</typeparam>
    /// <param name="configuration">The <see cref="IConfiguration" />to get the settings from.</param>
    /// <param name="configurationPath">The <see cref="IConfiguration" />section to get the settings from.</param>
    /// <param name="configureSettings">
    ///     An optional delegate to configure the settings. This is called after the settings are
    ///     loaded from the configuration.
    /// </param>
    /// <returns>The <see cref="T" />settings instance.</returns>
    public static T GetSettings<T>(IConfiguration configuration,
        string configurationPath,
        Action<T>? configureSettings = null) where T : new()
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrEmpty(configurationPath);

        return GetSettings(new T(), configuration, configurationPath, configureSettings);
    }

    /// <summary>
    ///     Gets the settings from the configuration.
    /// </summary>
    /// <typeparam name="T">Settings type</typeparam>
    /// <param name="settings">The settings instance to bind the configuration to.</param>
    /// <param name="configuration">The <see cref="IConfiguration" />to get the settings from.</param>
    /// <param name="configurationPath">The <see cref="IConfiguration" />section to get the settings from.</param>
    /// <param name="configureSettings">
    ///     An optional delegate to configure the settings. This is called after the settings are
    ///     loaded from the configuration.
    /// </param>
    /// <returns>The <see cref="T" />settings instance.</returns>
    public static T GetSettings<T>(T settings, IConfiguration configuration,
        string configurationPath,
        Action<T>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrEmpty(configurationPath);

        configuration.GetSection($"{configurationPath}:{Settings}").Bind(settings);
        configureSettings?.Invoke(settings);

        return settings;
    }


    /// <summary>
    ///     Gets the name or the default if the name is null, empty or whitespace.
    /// </summary>
    /// <param name="name">The spark name</param>
    /// <param name="defaultName">The default spark name </param>
    /// <returns></returns>
    public static string Name(string? name, string defaultName)
    {
        ArgumentException.ThrowIfNullOrEmpty(defaultName);

        return string.IsNullOrWhiteSpace(name) ? defaultName.Trim() : name.Trim();
    }

    /// <summary>
    ///     Gets the configuration path for the service.
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <param name="sectionPath">Section path</param>
    /// <returns></returns>
    public static string Path(string? serviceName, string sectionPath)
    {
        ArgumentNullException.ThrowIfNull(sectionPath);

        sectionPath = sectionPath.Trim();

        if (string.IsNullOrWhiteSpace(serviceName)) return sectionPath;
        serviceName = serviceName.Trim();
        var configPath = sectionPath == string.Empty ? serviceName : $"{sectionPath}:{serviceName}";

        return configPath;
    }
}