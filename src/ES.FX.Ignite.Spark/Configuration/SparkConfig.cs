using Microsoft.Extensions.Configuration;

namespace ES.FX.Ignite.Spark.Configuration;

public static class SparkConfig
{
    /// <summary>
    ///     Default configuration key for settings
    /// </summary>
    public const string SettingsKey = "Settings";


    /// <summary>
    ///     Gets the key or the default key if the key is null or empty.
    /// </summary>
    /// <param name="key">The spark key</param>
    /// <param name="defaultKey">The default spark key </param>
    /// <returns></returns>
    public static string Key(string? key, string defaultKey)
    {
        key = key?.Trim();
        defaultKey = defaultKey.Trim();

        return key ?? defaultKey;
    }

    public static string ConfigurationKey(string key, string section)
    {
        key = key.Trim();
        section = section.Trim();
        var configurationKey = section == string.Empty ? key : $"{section}:{key}";

        return configurationKey;
    }

    /// <summary>
    ///     Gets the settings from the configuration.
    /// </summary>
    /// <typeparam name="T">Settings type</typeparam>
    /// <param name="configuration">The <see cref="IConfiguration" />to get the settings from.</param>
    /// <param name="sectionKey">The <see cref="IConfiguration" />section to get the settings from.</param>
    /// <param name="configureSettings">
    ///     An optional delegate to configure the settings. This is called after the settings are
    ///     loaded from the configuration.
    /// </param>
    /// <returns>The <see cref="T" />settings instance.</returns>
    public static T GetSettings<T>(IConfiguration configuration,
        string sectionKey,
        Action<T>? configureSettings = null) where T : new() =>
        GetSettings(new T(), configuration, sectionKey, configureSettings);

    /// <summary>
    ///     Gets the settings from the configuration.
    /// </summary>
    /// <typeparam name="T">Settings type</typeparam>
    /// <param name="settings">The settings instance to bind the configuration to.</param>
    /// <param name="configuration">The <see cref="IConfiguration" />to get the settings from.</param>
    /// <param name="sectionKey">The <see cref="IConfiguration" />section to get the settings from.</param>
    /// <param name="configureSettings">
    ///     An optional delegate to configure the settings. This is called after the settings are
    ///     loaded from the configuration.
    /// </param>
    /// <returns>The <see cref="T" />settings instance.</returns>
    public static T GetSettings<T>(T settings, IConfiguration configuration,
        string sectionKey,
        Action<T>? configureSettings = null)
    {
        configuration.GetSection($"{sectionKey}:{SettingsKey}").Bind(settings);
        configureSettings?.Invoke(settings);

        return settings;
    }
}