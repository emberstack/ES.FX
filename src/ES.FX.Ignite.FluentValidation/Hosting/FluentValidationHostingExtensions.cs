using ES.FX.Ignite.FluentValidation.Configuration;
using ES.FX.Ignite.Spark;
using ES.FX.Ignite.Spark.Configuration;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Configuration;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Configuration;

namespace ES.FX.Ignite.FluentValidation.Hosting;

[PublicAPI]
public static class FluentValidationHostingExtensions
{
    /// <summary>
    ///     Registers <see cref="FluentValidation" /> services in the services provided by the
    ///     <paramref name="builder" />.
    ///     Enables AutoValidation for Endpoints and MVC.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="configureSettings">
    ///     An optional delegate that can be used for customizing settings. It's invoked after the
    ///     settings are read from the configuration.
    /// </param>
    /// <param name="configureAutoValidationEndpointsConfiguration">
    ///     An optional delegate that can be used for customizing the
    ///     <see cref="AutoValidationEndpointsConfiguration" />.
    /// </param>
    /// <param name="configureAutoValidationMvcConfiguration">
    ///     An optional delegate that can be used for customizing the
    ///     <see cref="AutoValidationMvcConfiguration" />.
    /// </param>
    /// <param name="configurationSectionPath">
    ///     The configuration section path. Default is
    ///     <see cref="FluentValidationSpark.ConfigurationSectionPath" />.
    /// </param>
    public static void AddIgniteFluentValidation(this IHostApplicationBuilder builder,
        Action<FluentValidationSparkSettings>? configureSettings = null,
        Action<AutoValidationEndpointsConfiguration>? configureAutoValidationEndpointsConfiguration = null,
        Action<AutoValidationMvcConfiguration>? configureAutoValidationMvcConfiguration = null,
        string configurationSectionPath = FluentValidationSpark.ConfigurationSectionPath)
    {
        builder.GuardSparkConfiguration(FluentValidationSpark.Name,
            $"{FluentValidationSpark.Name} already configured.");

        var settings = SparkConfig.GetSettings(builder.Configuration, configurationSectionPath, configureSettings);
        builder.Services.AddSingleton(settings);

        if (settings.EndpointsAutoValidationEnabled)
            ServiceCollectionExtensions
                .AddFluentValidationAutoValidation(builder.Services, configureAutoValidationEndpointsConfiguration);

        if (settings.MvcAutoValidationEnabled)
            SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions.ServiceCollectionExtensions
                .AddFluentValidationAutoValidation(builder.Services, configureAutoValidationMvcConfiguration);
    }
}