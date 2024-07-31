using ES.FX.Ignite.Spark;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Swashbuckle.Configuration;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace ES.FX.Ignite.Swashbuckle.Hosting;

[PublicAPI]
public static class SwashbuckleHostingExtensions
{
    /// <summary>
    ///     Registers <see cref="Swashbuckle" /> services in the services provided by the
    ///     <paramref name="builder" />.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="configureSettings">
    ///     An optional delegate that can be used for customizing settings. It's invoked after the
    ///     settings are read from the configuration.
    /// </param>
    /// <param name="configureSwaggerGenOptions">
    ///     An optional delegate that can be used for customizing the
    ///     <see cref="SwaggerGenOptions" />.
    /// </param>
    /// <param name="configurationSectionPath">
    ///     The configuration section path. Default is
    ///     <see cref="SwashbuckleSpark.ConfigurationSectionPath" />.
    /// </param>
    public static void IgniteSwashbuckle(this IHostApplicationBuilder builder,
        Action<SwashbuckleSparkSettings>? configureSettings = null,
        Action<SwaggerGenOptions>? configureSwaggerGenOptions = null,
        string configurationSectionPath = SwashbuckleSpark.ConfigurationSectionPath)
    {
        builder.GuardSparkConfiguration(SwashbuckleSpark.Name,
            SparkGuard.DefaultConfigurationErrorMessageGenerator(SwashbuckleSpark.Name));

        var settings = SparkConfig.GetSettings(builder.Configuration, configurationSectionPath, configureSettings);
        builder.Services.AddSingleton(settings);

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(configureSwaggerGenOptions);
    }

    /// <summary>
    ///     Uses the Ignite Swashbuckle Swagger and Swagger UI.
    /// </summary>
    /// <param name="app"> The <see cref="WebApplication" /> to configure the Ignite HealthChecks UI for.</param>
    /// <param name="configureSwaggerOptions">
    ///     An optional delegate that can be used for customizing the
    ///     <see cref="SwaggerOptions" />.
    /// </param>
    /// <param name="configureSwaggerUIOptions">
    ///     An optional delegate that can be used for customizing the
    ///     <see cref="SwaggerUIOptions" />.
    /// </param>
    public static void IgniteSwashbuckle(this WebApplication app,
        Action<SwaggerOptions>? configureSwaggerOptions = null,
        // ReSharper disable once InconsistentNaming
        Action<SwaggerUIOptions>? configureSwaggerUIOptions = null)
    {
        var settings = app.Services.GetRequiredService<SwashbuckleSparkSettings>();

        if (settings.SwaggerEnabled)
            app.UseSwagger(configureSwaggerOptions);

        if (settings.SwaggerUIEnabled)
            app.UseSwaggerUI(configureSwaggerUIOptions);
    }
}