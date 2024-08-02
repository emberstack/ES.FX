using ES.FX.Ignite.AspNetCore.HealthChecks.UI.Hosting;
using Microsoft.AspNetCore.Builder;
using ES.FX.Ignite.Spark.Exceptions;
using Microsoft.Extensions.Configuration;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.AspNetCore.HealthChecks.UI.Configuration;
using ES.FX.AspNetCore.HealthChecks.UI.HealthChecksEndpointRegistry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using ES.FX.Shared.Tests.Utils;

namespace ES.FX.Ignite.AspNetCore.HealthChecks.UI.Tests.Hosting
{
    public class HealthChecksUiHostingExtensionsTests
    {
        [Fact]
        public void IgniteHealthCheckUiDoesNotAllowReconfiguration()
        {
            var builder = WebApplication.CreateBuilder([]);

            builder.IgniteHealthChecksUi();

            Assert.Throws<SparkReconfigurationNotSupportedException>(() => builder.IgniteHealthChecksUi());
        }

        [Fact]
        public void IgniteHealthCheckUiShouldAddTheServices()
        {
            var builder = WebApplication.CreateBuilder([]);

            builder.IgniteHealthChecksUi();

            builder.Configuration.AddInMemoryCollection([
                 new KeyValuePair<string, string?>(
                $"{HealthChecksUiSpark.ConfigurationSectionPath}:{SparkConfig.Settings}:{ nameof(HealthChecksUiSparkSettings.EndpointEnabled)}",
                true.ToString())
             ]);

            var serviceProvider = builder.Build().Services;
            Assert.NotNull(serviceProvider.GetRequiredService<HealthChecksEndpointRegistryService>());
            Assert.NotNull(serviceProvider.GetRequiredService<HealthChecksUiSparkSettings>());
        }

        [Fact]
        public void CanOverride_Settings()
        {
            var builder = WebApplication.CreateBuilder([]);

            builder.Configuration.AddInMemoryCollection([
                 new KeyValuePair<string, string?>(
                $"{HealthChecksUiSpark.ConfigurationSectionPath}:{SparkConfig.Settings}:{ nameof(HealthChecksUiSparkSettings.EndpointEnabled)}",
                true.ToString())
             ]);

            builder.IgniteHealthChecksUi(configureSettings: ConfigureSettings);

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

        [Fact]
        public async Task FunctionalHealthChecksTestAPIPathIsWorking()
        {
            var client = WebApplicationFactoryUtils<HealthChecksTestHost>.GetClient();

            var response = await client.GetAsync(new HealthChecksUiSparkSettings().UiApiEndpointPath);
            Assert.True(response.IsSuccessStatusCode);
        }
    }
}
