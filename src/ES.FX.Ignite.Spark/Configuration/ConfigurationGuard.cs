using ES.FX.Ignite.Spark.Exceptions;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Spark.Configuration;

public static class KeyedConfigurationGuard
{
    public static string AlreadyConfiguredError(string key) => $"{key} already configured";
    private static string GuardConfigurationKey(string key) => $"{nameof(KeyedConfigurationGuard)}-{key}";

    /// <summary>
    ///     Prevent reconfiguration by throwing an exception if the key is already configured
    /// </summary>
    /// <param name="builder"> The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="key"> The key to guard</param>
    /// <param name="message"> The message to throw if the key is already configured</param>
    /// <exception cref="ReconfigurationNotSupportedException"></exception>
    public static void GuardConfigurationKey(this IHostApplicationBuilder builder, string key, string? message = null)
    {
        if (builder.ConfigurationKeyGuardSet(key))
            throw new ReconfigurationNotSupportedException(string.IsNullOrEmpty(message)
                ? AlreadyConfiguredError(key)
                : message);
        builder.Properties.Add(GuardConfigurationKey(key), string.Empty);
    }


    /// <summary>
    ///     Checks if the spark is already guarded
    /// </summary>
    /// <param name="builder"> The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="key"> The key to guard</param>
    public static bool ConfigurationKeyGuardSet(this IHostApplicationBuilder builder, string key) =>
        builder.Properties.ContainsKey(GuardConfigurationKey(key));
}