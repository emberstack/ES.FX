using ES.FX.Ignite.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ES.FX.Ignite.Hosting;
using Microsoft.Extensions.Hosting;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Exceptions;

namespace ES.FX.Ignite.Tests.Hosting
{
    public class IgniteHostingExtensionsTests
    {
        [Fact]
        public void Ignite_WhenCalled_ShouldAddServices()
        {
            var builder = Host.CreateEmptyApplicationBuilder(null);

            builder.Ignite();

            var serviceProvider = builder.Build().Services;
            Assert.NotNull(serviceProvider.GetRequiredService<IgniteSettings>());
        }

        [Fact]
        public void Ignite_WhenCalled_ShouldAddAdditionalSettingsFiles()
        {
            var builder = Host.CreateEmptyApplicationBuilder(null);

            builder.Configuration.AddInMemoryCollection([
                new KeyValuePair<string, string?>(
                $"{IgniteConfigurationSections.Ignite}:{SparkConfig.Settings}:Configuration:AdditionalJsonSettingsFiles:0",
                "testAdditionalAppSettings.json")
            ]);

            builder.Ignite();

            Assert.Equal("ExtraPropertyValue", builder.Configuration.GetValue<string>("ExtraPropertyHeader:ExtraProperty"));
        }

        [Fact]
        public void Ignite_Should_be_allowed_once()
        {
            var builder = Host.CreateEmptyApplicationBuilder(null);

            builder.Ignite();

            Assert.Throws<SparkReconfigurationNotSupportedException>(() => builder.Ignite());
        }
    }
}
