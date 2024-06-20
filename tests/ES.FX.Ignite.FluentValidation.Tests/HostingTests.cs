using ES.FX.Ignite.FluentValidation.Configuration;
using ES.FX.Ignite.FluentValidation.Hosting;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Results;

namespace ES.FX.Ignite.FluentValidation.Tests;

public class HostingTests
{
    [Fact]
    public void CanOverride_Settings()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{FluentValidationSpark.ConfigurationSectionPath}:{SparkConfig.Settings}:{nameof(FluentValidationSparkSettings.MvcAutoValidationEnabled)}",
                true.ToString())
        ]);

        builder.IgniteFluentValidation(ConfigureSettings);

        var app = builder.Build();

        var resolvedSettings = app.Services.GetRequiredService<FluentValidationSparkSettings>();
        Assert.False(resolvedSettings.EndpointsAutoValidationEnabled);

        return;

        static void ConfigureSettings(FluentValidationSparkSettings settings)
        {
            //Settings should have correct value from configuration
            Assert.True(settings.EndpointsAutoValidationEnabled);


            //Change the settings
            settings.EndpointsAutoValidationEnabled = false;
        }
    }

    [Fact]
    public void IgniteDoesNotAllowReconfiguration()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.IgniteFluentValidation();

        Assert.Throws<ReconfigurationNotSupportedException>(() => builder.IgniteFluentValidation());
    }

    [Fact]
    public void IgniteShouldAddTheServicesEndpointsAutoValidation()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{FluentValidationSpark.ConfigurationSectionPath}:{SparkConfig.Settings}:{nameof(FluentValidationSparkSettings.EndpointsAutoValidationEnabled)}",
                true.ToString())
        ]);

        builder.IgniteFluentValidation();

        var serviceProvider = builder.Build().Services;
        Assert.NotNull(serviceProvider.GetRequiredService<FluentValidationSparkSettings>());
        Assert.NotNull(serviceProvider.GetService(typeof(IFluentValidationAutoValidationResultFactory)));
    }

    [Fact]
    public void IgniteShouldAddTheServicesMvcAutoValidation()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{FluentValidationSpark.ConfigurationSectionPath}:{SparkConfig.Settings}:{nameof(FluentValidationSparkSettings.MvcAutoValidationEnabled)}",
                true.ToString())
        ]);

        builder.IgniteFluentValidation();

        var serviceProvider = builder.Build().Services;
        Assert.NotNull(serviceProvider.GetRequiredService<FluentValidationSparkSettings>());
        Assert.NotNull(serviceProvider.GetService(typeof(IFluentValidationAutoValidationResultFactory)));
    }
}