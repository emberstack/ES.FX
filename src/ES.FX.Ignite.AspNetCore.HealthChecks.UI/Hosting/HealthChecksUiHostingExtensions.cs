using ES.FX.Additions.AspNetCore.HealthChecks.UI.HealthChecksEndpointRegistry;
using ES.FX.Ignite.AspNetCore.HealthChecks.UI.Configuration;
using ES.FX.Ignite.AspNetCore.HealthChecks.UI.IgniteTheme;
using ES.FX.Ignite.AspNetCore.HealthChecks.UI.Interceptors;
using ES.FX.Ignite.Spark.Configuration;
using HealthChecks.UI.Configuration;
using HealthChecks.UI.Core;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.Ignite.AspNetCore.HealthChecks.UI.Hosting;

[PublicAPI]
public static class HealthChecksUiHostingExtensions
{
    /// <summary>
    ///     Registers the Ignite HealthChecks UI services
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="configureSettings">
    ///     An optional delegate that can be used for customizing settings. It's invoked after the
    ///     settings are read from the configuration.
    /// </param>
    /// <param name="configureHealthChecksUiSettings">
    ///     An optional delegate that can be used for customizing health checks UI
    ///     settings. It's invoked after the settings are read from the configuration.
    /// </param>
    /// <param name="configureHealthChecksUiBuilder">
    ///     An optional delegate that can be used for customizing health checks UI
    ///     builder. It's invoked after the settings are read from the configuration.
    /// </param>
    /// <param name="configurationSectionPath">
    ///     The configuration section path. Default is
    ///     <see cref="HealthChecksUiSpark.ConfigurationSectionPath" />.
    /// </param>
    public static void IgniteHealthChecksUi(this WebApplicationBuilder builder,
        Action<HealthChecksUiSparkSettings>? configureSettings = null,
        Action<Settings>? configureHealthChecksUiSettings = null,
        Action<HealthChecksUIBuilder>? configureHealthChecksUiBuilder = null,
        string configurationSectionPath = HealthChecksUiSpark.ConfigurationSectionPath)
    {
        builder.GuardConfigurationKey(HealthChecksUiSpark.Name);

        var settings = SparkConfig.GetSettings(builder.Configuration, configurationSectionPath, configureSettings);
        builder.Services.AddSingleton(settings);


        var healthChecksUiBuilder = builder.Services.AddHealthChecksUI(healthChecksUiSettings =>
        {
            builder.Configuration.GetSection(configurationSectionPath)
                .Bind(healthChecksUiSettings,
                    binderOptions => binderOptions.BindNonPublicProperties = true);
            configureHealthChecksUiSettings?.Invoke(healthChecksUiSettings);
        });

        (configureHealthChecksUiBuilder ?? ConfigureDefaultHealthChecksUiBuilder).Invoke(healthChecksUiBuilder);


        builder.AddHealthChecksEndpointRegistry();

        builder.Services.AddTransient<IHealthCheckCollectorInterceptor, IPv6LoopbackAddressInterceptor>();

        return;

        static void ConfigureDefaultHealthChecksUiBuilder(HealthChecksUIBuilder healthChecksUiBuilder) =>
            healthChecksUiBuilder.AddInMemoryStorage();
    }


    /// <summary>
    ///     Uses the Ignite HealthChecks UI
    /// </summary>
    /// <param name="app"> The <see cref="WebApplication" /> to configure the Ignite HealthChecks UI for.</param>
    /// <param name="configureHealthChecksUiOptions">
    ///     An optional delegate that can be used for customizing health checks UI
    ///     options.
    /// </param>
    public static void IgniteHealthChecksUi(this WebApplication app,
        Action<Options>? configureHealthChecksUiOptions = null)
    {
        var settings = app.Services.GetRequiredService<HealthChecksUiSparkSettings>();

        if (!settings.EndpointEnabled) return;


        app.UseMiddleware<ThemeMiddleware>();


        //TODO: Broken UI polling. Replace this with app.UseHealthChecksUI when this issue is fixed: https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks/pull/1782
        app.UseRouting().UseEndpoints(config =>
            config.MapHealthChecksUI(options =>
            {
                options.UIPath = settings.UiEndpointPath;
                options.ApiPath = settings.UiApiEndpointPath;

                options.WebhookPath = settings.UiWebhookEndpointPath;
                options.ResourcesPath = $"{options.UIPath}/{ThemeMiddleware.IgniteThemeResourcesPath}";
                options.UseRelativeApiPath = false;
                options.UseRelativeResourcesPath = false;
                options.UseRelativeWebhookPath = false;

                options.AsideMenuOpened = false;

                configureHealthChecksUiOptions?.Invoke(options);
            }));
    }
}