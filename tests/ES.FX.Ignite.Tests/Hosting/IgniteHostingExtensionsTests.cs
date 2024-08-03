using System.Text.Json.Serialization;
using ES.FX.Ignite.Configuration;
using ES.FX.Ignite.Hosting;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Exceptions;
using HealthChecks.ApplicationStatus;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using Polly;

namespace ES.FX.Ignite.Tests.Hosting;

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
                $"{IgniteConfigurationSections.Ignite}:{SparkConfig.Settings}:{nameof(IgniteSettings.Configuration)}:{nameof(IgniteConfigurationSettings.AdditionalJsonSettingsFiles)}:0",
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

        Assert.Throws<ReconfigurationNotSupportedException>(() => builder.Ignite());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanDisableEnableOpenTelemetry(bool enable)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{IgniteConfigurationSections.Ignite}:{SparkConfig.Settings}:{nameof(IgniteSettings.OpenTelemetry)}:{nameof(IgniteOpenTelemetrySettings.Enabled)}",
                enable.ToString())
        ]);

        builder.Ignite();

        var app = builder.Build();
        Assert.Equal(app.Services.GetService<TracerProvider>() != null, enable);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanDisableEnableHealthChecks(bool enable)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{IgniteConfigurationSections.Ignite}:{SparkConfig.Settings}:{nameof(IgniteSettings.HealthChecks)}:{nameof(IgniteHealthChecksSettings.ApplicationStatusCheckEnabled)}",
                enable.ToString())
        ]);

        builder.Ignite();

        var app = builder.Build();

        Assert.Equal(app.Services.GetService<ApplicationStatusHealthCheck>() != null, enable);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanDisableEnableStandardResilienceHandler(bool enable)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{IgniteConfigurationSections.Ignite}:{SparkConfig.Settings}:{nameof(IgniteSettings.HttpClient)}:{nameof(IgniteHttpClientSettings.StandardResilienceHandlerEnabled)}",
                enable.ToString())
        ]);

        builder.Ignite();

        var app = builder.Build();

        Assert.Equal(app.Services.GetService<ResiliencePipelineBuilder>() != null, enable);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanDisableEnableEndpointsApiExplorer(bool enable)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{IgniteConfigurationSections.Ignite}:{SparkConfig.Settings}:{nameof(IgniteSettings.AspNetCore)}:{nameof(IgniteAspNetCoreSettings.EndpointsApiExplorerEnabled)}",
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
                $"{IgniteConfigurationSections.Ignite}:{SparkConfig.Settings}:{nameof(IgniteSettings.AspNetCore)}:{nameof(IgniteAspNetCoreSettings.ProblemDetailsEnabled)}",
                enable.ToString())
        ]);
        builder.Ignite();

        var app = builder.Build();
        Assert.Equal(app.Services.GetService<IProblemDetailsService>() != null, enable);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanDisableEnableJsonStringEnumConverter(bool enable)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{IgniteConfigurationSections.Ignite}:{SparkConfig.Settings}:{nameof(IgniteSettings.AspNetCore)}:{nameof(IgniteAspNetCoreSettings.JsonStringEnumConverterEnabled)}",
                enable.ToString())
        ]);
        builder.Ignite();

        var app = builder.Build();
        var jsonOptionsService = app.Services.GetRequiredService<IOptions<JsonOptions>>();
        Assert.Equal(
            jsonOptionsService.Value.SerializerOptions.Converters.Any(c =>
                c.GetType() == typeof(JsonStringEnumConverter)), enable);
    }
}