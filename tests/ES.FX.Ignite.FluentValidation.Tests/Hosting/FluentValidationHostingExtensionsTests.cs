using Microsoft.Extensions.Hosting;
using ES.FX.Ignite.FluentValidation.Hosting;
using ES.FX.Ignite.Spark.Exceptions;
using Microsoft.Extensions.Configuration;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.FluentValidation.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Results;

namespace ES.FX.Ignite.FluentValidation.Tests.Hosting
{
    public class FluentValidationHostingExtensions
    {
        [Fact]
        public void IgniteDoesNotAllowReconfiguration()
        {
            var builder = Host.CreateEmptyApplicationBuilder(null);

            builder.IgniteFluentValidation();

            Assert.Throws<SparkReconfigurationNotSupportedException>(() => builder.IgniteFluentValidation());
        }

        [Fact]
        public void IgniteShouldAddTheServicesEndpointsAutoValidation()
        {
            var builder = Host.CreateEmptyApplicationBuilder(null);

            builder.Configuration.AddInMemoryCollection([
                 new KeyValuePair<string, string?>(
                $"{FluentValidationSpark.ConfigurationSectionPath}:{SparkConfig.Settings}:{ nameof(FluentValidationSparkSettings.EndpointsAutoValidationEnabled)}",
                true.ToString())
             ]);

            builder.IgniteFluentValidation();

            var serviceProvider = builder.Build().Services;
            Assert.NotNull(serviceProvider.GetRequiredService<FluentValidationSparkSettings>());
            Assert.NotNull(serviceProvider.GetService(typeof(IFluentValidationAutoValidationResultFactory)));
        }

        [Fact]
        public void IgniteShouldAddTheServicesMVCAutoValidation()
        {
            var builder = Host.CreateEmptyApplicationBuilder(null);

            builder.Configuration.AddInMemoryCollection([
                 new KeyValuePair<string, string?>(
                $"{FluentValidationSpark.ConfigurationSectionPath}:{SparkConfig.Settings}:{ nameof(FluentValidationSparkSettings.MvcAutoValidationEnabled)}",
                true.ToString())
             ]);

            builder.IgniteFluentValidation();

            var serviceProvider = builder.Build().Services;
            Assert.NotNull(serviceProvider.GetRequiredService<FluentValidationSparkSettings>());
            Assert.NotNull(serviceProvider.GetService(typeof(IFluentValidationAutoValidationResultFactory)));
        }

        [Fact]
        public void CanOverride_Settings()
        {
            var builder = Host.CreateEmptyApplicationBuilder(null);

            builder.Configuration.AddInMemoryCollection([
                 new KeyValuePair<string, string?>(
                $"{FluentValidationSpark.ConfigurationSectionPath}:{SparkConfig.Settings}:{ nameof(FluentValidationSparkSettings.MvcAutoValidationEnabled)}",
                true.ToString())
             ]);

            builder.IgniteFluentValidation(configureSettings: ConfigureSettings);

            var app = builder.Build();

            var resolvedSettings = app.Services.GetRequiredService<FluentValidationSparkSettings>();
            Assert.False(resolvedSettings.EndpointsAutoValidationEnabled);

            return;

            void ConfigureSettings(FluentValidationSparkSettings settings)
            {
                //Settings should have correct value from configuration
                Assert.True(settings.EndpointsAutoValidationEnabled);


                //Change the settings
                settings.EndpointsAutoValidationEnabled = false;
            }
        }

    }
}
