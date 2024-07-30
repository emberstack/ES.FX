using ES.FX.Ignite.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ES.FX.Ignite.Hosting;
using Microsoft.Extensions.Hosting;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Exceptions;
using OpenTelemetry.Trace;
using HealthChecks.ApplicationStatus;
using Polly;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanDisableEnableOpenTelemetry(bool enable)
        {
            var builder = Host.CreateEmptyApplicationBuilder(null);

            builder.Configuration.AddInMemoryCollection([
                new KeyValuePair<string, string?>(
                $"{IgniteConfigurationSections.Ignite}:{SparkConfig.Settings}:OpenTelemetry:Enabled",
                enable.ToString())
            ]);

            builder.Ignite();

            Assert.Equal(builder.Services.Any(s => s.ServiceType == typeof(TracerProvider)), enable);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanDisableEnableHealthChecks(bool enable)
        {
            var builder = Host.CreateEmptyApplicationBuilder(null);

            builder.Configuration.AddInMemoryCollection([
                new KeyValuePair<string, string?>(
                $"{IgniteConfigurationSections.Ignite}:{SparkConfig.Settings}:HealthChecks:ApplicationStatusCheckEnabled",
                enable.ToString())
            ]);

            builder.Ignite();

            Assert.Equal(builder.Services.Any(s => s.ServiceType == typeof(ApplicationStatusHealthCheck)), enable);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanDisableEnableStandardResilienceHandler(bool enable)
        {
            var builder = Host.CreateEmptyApplicationBuilder(null);

            builder.Configuration.AddInMemoryCollection([
                new KeyValuePair<string, string?>(
                $"{IgniteConfigurationSections.Ignite}:{SparkConfig.Settings}:HttpClient:StandardResilienceHandlerEnabled",
                enable.ToString())
            ]);

            builder.Ignite();

            Assert.Equal(builder.Services.Any(s => s.ServiceType == typeof(ResiliencePipelineBuilder)), enable);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanDisableEnableEndpointsApiExplorer(bool enable)
        {
            var builder = Host.CreateEmptyApplicationBuilder(null);

            builder.Configuration.AddInMemoryCollection([
                new KeyValuePair<string, string?>(
                $"{IgniteConfigurationSections.Ignite}:{SparkConfig.Settings}:AspNetCore:EndpointsApiExplorerEnabled",
                enable.ToString())
            ]);

            builder.Ignite();

            Assert.Equal(builder.Services.Any(s => s.ServiceType == typeof(IApiDescriptionProvider)), enable);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanDisableEnableProblemDetails(bool enable)
        {
            var builder = Host.CreateEmptyApplicationBuilder(null);

            builder.Configuration.AddInMemoryCollection([
                new KeyValuePair<string, string?>(
                $"{IgniteConfigurationSections.Ignite}:{SparkConfig.Settings}:AspNetCore:ProblemDetailsEnabled",
                enable.ToString())
            ]);
            builder.Ignite();

            Assert.Equal(builder.Services.Any(s => s.ServiceType == typeof(IProblemDetailsService)), enable);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanDisableEnableJsonStringEnumConverter(bool enable)
        {
            var builder = Host.CreateEmptyApplicationBuilder(null);

            builder.Configuration.AddInMemoryCollection([
                new KeyValuePair<string, string?>(
                $"{IgniteConfigurationSections.Ignite}:{SparkConfig.Settings}:AspNetCore:JsonStringEnumConverterEnabled",
                enable.ToString())
            ]);
            builder.Ignite();

            var services = builder.Build().Services;
            var jsonOptionsService = services.GetRequiredService<IOptions<JsonOptions>>();
            Assert.Equal(jsonOptionsService.Value.SerializerOptions.Converters.Any(c => c.GetType() == typeof(JsonStringEnumConverter)), enable);
        }
    }
}
