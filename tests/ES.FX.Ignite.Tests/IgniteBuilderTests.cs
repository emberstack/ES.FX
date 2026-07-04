using System.Text.Json.Serialization;
using ES.FX.Ignite.Configuration;
using ES.FX.Ignite.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace ES.FX.Ignite.Tests;

/// <summary>
///     Covers the pre-build (phase 1)
///     <see cref="IgniteHostingExtensions.Ignite(IHostApplicationBuilder, Action{IgniteSettings}?, string)" />
///     activation: argument guards, configuration binding, the <c>configureSettings</c> delegate, and the
///     conditional service registrations it performs based on <see cref="IgniteSettings" />.
/// </summary>
public class IgniteBuilderTests
{
    private static IHostApplicationBuilder CreateBuilder(
        IEnumerable<KeyValuePair<string, string?>>? configuration = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        if (configuration is not null)
            builder.Configuration.AddInMemoryCollection(configuration);
        return builder;
    }

    [Fact]
    public void Ignite_NullBuilder_Throws()
    {
        IHostApplicationBuilder builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.Ignite());
    }

    [Fact]
    public void Ignite_RegistersSettingsSingleton()
    {
        var builder = CreateBuilder();

        builder.Ignite();

        using var provider = builder.Services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IgniteSettings>();
        Assert.NotNull(settings);
        // Same instance every resolution (singleton).
        Assert.Same(settings, provider.GetRequiredService<IgniteSettings>());
    }

    [Fact]
    public void Ignite_ConfigureSettingsDelegate_IsInvokedAfterBinding()
    {
        // Configuration sets a value; the delegate must observe the bound value and be able to override it.
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Ignite:Settings:OpenTelemetry:UseAzureMonitor"] = "true"
        });

        bool? observedFromConfig = null;
        builder.Ignite(settings =>
        {
            observedFromConfig = settings.OpenTelemetry.UseAzureMonitor;
            settings.OpenTelemetry.UseAzureMonitor = false;
        });

        using var provider = builder.Services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IgniteSettings>();

        Assert.True(observedFromConfig); // delegate saw the value bound from configuration
        Assert.False(settings.OpenTelemetry.UseAzureMonitor); // delegate override won
    }

    [Fact]
    public void Ignite_BindsSettingsFromConfigurationSection()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Ignite:Settings:AspNetCore:HealthChecks:ReadinessEndpointPath"] = "/custom/ready",
            ["Ignite:Settings:HttpClient:StandardResilienceHandlerEnabled"] = "false"
        });

        builder.Ignite();

        using var provider = builder.Services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IgniteSettings>();
        Assert.Equal("/custom/ready", settings.AspNetCore.HealthChecks.ReadinessEndpointPath);
        Assert.False(settings.HttpClient.StandardResilienceHandlerEnabled);
    }

    [Fact]
    public void Ignite_CustomConfigurationSectionPath_IsHonored()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["MyIgnite:Settings:AspNetCore:HealthChecks:LivenessEndpointPath"] = "/alt/live"
        });

        builder.Ignite(configurationSectionPath: "MyIgnite");

        using var provider = builder.Services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IgniteSettings>();
        Assert.Equal("/alt/live", settings.AspNetCore.HealthChecks.LivenessEndpointPath);
    }

    [Fact]
    public void Ignite_AlwaysRegisters_HealthChecks_And_HttpClientFactory()
    {
        var builder = CreateBuilder();

        builder.Ignite();

        using var provider = builder.Services.BuildServiceProvider();
        // Health checks service is always registered.
        Assert.NotNull(provider.GetService<HealthCheckService>());
        // HttpClient factory is always registered (AddHttpClient()).
        Assert.NotNull(provider.GetService<IHttpClientFactory>());
    }

    [Fact]
    public void Ignite_ForwardedHeadersEnabled_ConfiguresForwardedHeadersOptions()
    {
        var builder = CreateBuilder();

        builder.Ignite(); // ForwardedHeadersEnabled defaults to true

        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        Assert.Equal(ForwardedHeaders.All, options.ForwardedHeaders);
        Assert.False(options.RequireHeaderSymmetry);
        Assert.Empty(options.KnownProxies);
        Assert.Empty(options.KnownIPNetworks);
    }

    [Fact]
    public void Ignite_ForwardedHeadersDisabled_DoesNotApplyIgniteConfiguration()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Ignite:Settings:AspNetCore:ForwardedHeadersEnabled"] = "false"
        });

        builder.Ignite();

        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        // With Ignite's configuration NOT applied, ForwardedHeaders is not set to All (the framework
        // default is None), proving the disabled branch skipped Ignite's Configure call.
        Assert.NotEqual(ForwardedHeaders.All, options.ForwardedHeaders);
        Assert.Equal(ForwardedHeaders.None, options.ForwardedHeaders);
    }

    [Fact]
    public void Ignite_ProblemDetailsEnabled_RegistersProblemDetailsService()
    {
        var builder = CreateBuilder();

        builder.Ignite(); // AddProblemDetails defaults to true

        using var provider = builder.Services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IProblemDetailsService>());
    }

    [Fact]
    public void Ignite_ProblemDetailsDisabled_DoesNotRegisterProblemDetailsService()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Ignite:Settings:AspNetCore:AddProblemDetails"] = "false"
        });

        builder.Ignite();

        using var provider = builder.Services.BuildServiceProvider();
        Assert.Null(provider.GetService<IProblemDetailsService>());
    }

    [Fact]
    public void Ignite_JsonStringEnumConverterEnabled_AddsConverterToHttpJsonOptions()
    {
        var builder = CreateBuilder();

        builder.Ignite(); // JsonStringEnumConverterEnabled defaults to true

        using var provider = builder.Services.BuildServiceProvider();
        var jsonOptions = provider.GetRequiredService<IOptions<JsonOptions>>().Value;
        Assert.Contains(jsonOptions.SerializerOptions.Converters,
            c => c is JsonStringEnumConverter);
    }

    [Fact]
    public void Ignite_JsonStringEnumConverterDisabled_DoesNotAddConverter()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Ignite:Settings:AspNetCore:JsonStringEnumConverterEnabled"] = "false"
        });

        builder.Ignite();

        using var provider = builder.Services.BuildServiceProvider();
        var jsonOptions = provider.GetRequiredService<IOptions<JsonOptions>>().Value;
        Assert.DoesNotContain(jsonOptions.SerializerOptions.Converters,
            c => c is JsonStringEnumConverter);
    }

    [Fact]
    public void Ignite_OpenTelemetryEnabled_RegistersMeterProviderAndTracerProvider()
    {
        var builder = CreateBuilder();

        builder.Ignite(); // OpenTelemetry.Enabled defaults to true

        using var provider = builder.Services.BuildServiceProvider();
        // AddOpenTelemetry().WithMetrics/.WithTracing registers the providers.
        Assert.NotNull(provider.GetService<MeterProvider>());
        Assert.NotNull(provider.GetService<TracerProvider>());
    }

    [Fact]
    public void Ignite_OpenTelemetryDisabled_DoesNotRegisterProviders()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Ignite:Settings:OpenTelemetry:Enabled"] = "false"
        });

        builder.Ignite();

        using var provider = builder.Services.BuildServiceProvider();
        Assert.Null(provider.GetService<MeterProvider>());
        Assert.Null(provider.GetService<TracerProvider>());
    }
}