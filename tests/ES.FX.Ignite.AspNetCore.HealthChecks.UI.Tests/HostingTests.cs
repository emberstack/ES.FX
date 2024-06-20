using ES.FX.Additions.AspNetCore.HealthChecks.UI.HealthChecksEndpointRegistry;
using ES.FX.Ignite.AspNetCore.HealthChecks.UI.Configuration;
using ES.FX.Ignite.AspNetCore.HealthChecks.UI.Hosting;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.Ignite.AspNetCore.HealthChecks.UI.Tests;

public class HostingTests
{
    [Fact]
    public void CanIgnite()
    {
        var builder = WebApplication.CreateBuilder([]);

        builder.IgniteHealthChecksUi();

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{HealthChecksUiSpark.ConfigurationSectionPath}:{SparkConfig.Settings}:{nameof(HealthChecksUiSparkSettings.EndpointEnabled)}",
                true.ToString())
        ]);

        var serviceProvider = builder.Build().Services;
        Assert.NotNull(serviceProvider.GetRequiredService<HealthChecksEndpointRegistryService>());
        Assert.NotNull(serviceProvider.GetRequiredService<HealthChecksUiSparkSettings>());
    }


    [Fact]
    public void CanIgnite_Once()
    {
        var builder = WebApplication.CreateBuilder([]);

        builder.IgniteHealthChecksUi();

        Assert.Throws<ReconfigurationNotSupportedException>(() => builder.IgniteHealthChecksUi());
    }


    [Fact]
    public void CanOverride_Settings()
    {
        var builder = WebApplication.CreateBuilder([]);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{HealthChecksUiSpark.ConfigurationSectionPath}:{SparkConfig.Settings}:{nameof(HealthChecksUiSparkSettings.EndpointEnabled)}",
                true.ToString())
        ]);

        builder.IgniteHealthChecksUi(ConfigureSettings);

        var app = builder.Build();

        var resolvedSettings = app.Services.GetRequiredService<HealthChecksUiSparkSettings>();
        Assert.False(resolvedSettings.EndpointEnabled);

        return;

        void ConfigureSettings(HealthChecksUiSparkSettings settings)
        {
            //Settings should have correct value from configuration
            Assert.True(settings.EndpointEnabled);


            //Change the settings
            settings.EndpointEnabled = false;
        }
    }
}