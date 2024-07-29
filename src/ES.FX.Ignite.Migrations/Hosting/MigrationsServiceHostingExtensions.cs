using ES.FX.Ignite.Migrations.Configuration;
using ES.FX.Ignite.Migrations.Service;
using ES.FX.Ignite.Spark;
using ES.FX.Ignite.Spark.Configuration;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Migrations.Hosting;

[PublicAPI]
public static class MigrationsServiceHostingExtensions
{
    /// <summary>
    ///     Adds the <see cref="MigrationsService" /> to the <see cref="IHostBuilder" />.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="configureSettings">
    ///     An optional delegate that can be used for customizing settings. It's invoked after the
    ///     settings are read from the configuration.
    /// </param>
    /// <param name="configurationSectionPath">
    ///     The configuration section path. Default is
    ///     <see cref="MigrationsServiceSpark.ConfigurationSectionPath" />.
    /// </param>
    public static void IgniteMigrationsService(this IHostApplicationBuilder builder,
        Action<MigrationsServiceSparkSettings>? configureSettings = null,
        string configurationSectionPath = MigrationsServiceSpark.ConfigurationSectionPath)
    {
        builder.GuardSparkConfiguration($"{MigrationsServiceSpark.Name}",
            SparkGuard.GuardSparkDefaultConfigurationErrorMessageGenerator(MigrationsServiceSpark.Name));

        var settings = SparkConfig.GetSettings(builder.Configuration, configurationSectionPath, configureSettings);
        builder.Services.AddSingleton(settings);

        builder.Services.AddHostedService<MigrationsService>();
    }
}