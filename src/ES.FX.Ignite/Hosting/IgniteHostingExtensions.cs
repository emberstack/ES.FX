using System.Text.Json.Serialization;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using ES.FX.Ignite.Configuration;
using ES.FX.Ignite.OpenTelemetry.AspNetCore;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.HealthChecks;
using ES.FX.Microsoft.AspNetCore.Middleware;
using HealthChecks.ApplicationStatus.DependencyInjection;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ES.FX.Ignite.Hosting;

[PublicAPI]
public static class IgniteHostingExtensions
{
    public static void Ignite(this IHostApplicationBuilder builder,
        Action<IgniteSettings>? configureSettings = null,
        string configurationSectionPath = IgniteConfigurationSections.Ignite)
    {
        builder.GuardConfigurationKey(nameof(FX.Ignite));

        var settings = SparkConfig.GetSettings(builder.Configuration, configurationSectionPath, configureSettings);
        builder.Services.AddSingleton(settings);

        AddConfiguration(builder, settings.Configuration);

        AddOpenTelemetry(builder, settings.OpenTelemetry);

        AddHealthChecks(builder, settings.HealthChecks);

        AddHttpClient(builder, settings.HttpClient);

        AddAspNetServices(builder, settings.AspNetCore);
    }

    private static void AddConfiguration(IHostApplicationBuilder builder, IgniteConfigurationSettings settings)
    {
        // Add additional configuration files
        // Useful when mounting a configuration file from a secret manager or a configuration provider.
        foreach (var appSettingsFile in settings.AdditionalJsonSettingsFiles)
            builder.Configuration
                .AddJsonFile(appSettingsFile, true, true);

        // Add additional app settings overrides
        // Useful when mounting a configuration file from a secret manager or a configuration provider.
        foreach (var appSettingsOverride in settings.AdditionalJsonAppSettingsOverrides)
            builder.Configuration
                .AddJsonFile($"appsettings.{builder.Environment}.{appSettingsOverride}.json",
                    true, true);
    }

    private static void AddOpenTelemetry(IHostApplicationBuilder builder, IgniteOpenTelemetrySettings settings)
    {
        if (!settings.Enabled) return;

        if (settings.LoggingEnabled)
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = settings.LoggingIncludeFormattedMessage;
                logging.IncludeScopes = settings.LoggingIncludeScopes;
            });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(builder.Environment.ApplicationName))
            .WithMetrics(metrics =>
            {
                if (settings.RuntimeMetricsEnabled) metrics.AddRuntimeInstrumentation();
                if (settings.HttpClientMetricsEnabled) metrics.AddHttpClientInstrumentation();
                if (settings.AspNetCoreMetricsEnabled) metrics.AddAspNetCoreInstrumentation();
            })
            .WithTracing(tracing =>
            {
                if (settings.HttpClientTracingEnabled) tracing.AddHttpClientInstrumentation();
                if (settings.AspNetCoreTracingEnabled)
                    tracing.AddAspNetCoreInstrumentation(options =>
                    {
                        if (settings.AspNetCoreTracingHealthChecksRequestsFiltered)
                            options.Filter = IgnoreHealthChecksRequests.Filter;
                    });
            });

        if (settings.OtlpExporterEnabled) builder.Services.AddOpenTelemetry().UseOtlpExporter();
        if (settings.AzureMonitorExporterEnabled) builder.Services.AddOpenTelemetry().UseAzureMonitor();
    }

    private static void AddHealthChecks(IHostApplicationBuilder builder, IgniteHealthChecksSettings settings)
    {
        var healthCheckKey = builder.Environment.ApplicationName;
        var healthCheckBuilder = builder.Services.AddHealthChecks();
        if (settings.ApplicationStatusCheckEnabled)
            healthCheckBuilder.AddApplicationStatus(healthCheckKey, tags: [HealthChecksTags.Live, nameof(Host)]);
    }

    private static void AddHttpClient(IHostApplicationBuilder builder, IgniteHttpClientSettings settings)
    {
        if (settings.StandardResilienceHandlerEnabled)
            builder.Services.ConfigureHttpClientDefaults(http =>
            {
                // Turn on resilience by default
                http.AddStandardResilienceHandler();
            });

        builder.Services.AddHttpClient();
    }

    private static void AddAspNetServices(IHostApplicationBuilder builder, IgniteAspNetCoreSettings settings)
    {
        if (settings.ForwardedHeadersEnabled)
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.RequireHeaderSymmetry = false;
                options.ForwardedHeaders = ForwardedHeaders.All;
                options.ForwardLimit = null;
                options.KnownProxies.Clear();
                options.KnownNetworks.Clear();
            });

        if (settings.EndpointsApiExplorerEnabled)
            builder.Services.AddEndpointsApiExplorer();

        if (settings.ProblemDetailsEnabled)
            builder.Services.AddProblemDetails();

        if (settings.JsonStringEnumConverterEnabled)
        {
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
            builder.Services.Configure<JsonOptions>(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        }
    }


    public static IHost Ignite(this IHost host)
    {
        var settings = host.Services.GetRequiredService<IgniteSettings>();

        if (host is WebApplication app)
        {
            UseQueryStringToHeaderMiddleware(app, settings.AspNetCore);
            UseForwardedHeaders(app, settings.AspNetCore);
            UseHealthChecks(app, settings.HealthChecks);
        }

        return host;
    }

    private static void UseQueryStringToHeaderMiddleware(WebApplication app, IgniteAspNetCoreSettings settings)
    {
        if (!settings.QueryStringToHeaderMiddlewareEnabled) return;
        app.UseMiddleware<QueryStringToHeaderMiddleware>();
    }

    private static void UseForwardedHeaders(WebApplication app, IgniteAspNetCoreSettings settings)
    {
        if (!settings.ForwardedHeadersEnabled) return;
        app.UseForwardedHeaders();
    }

    private static void UseHealthChecks(WebApplication app, IgniteHealthChecksSettings settings)
    {
        if (!settings.EndpointEnabled) return;

        //Readiness checks are used to determine if the app is ready to accept traffic after starting
        //All health checks must pass for app to be considered ready
        var readinessHealthCheckOptions = new HealthCheckOptions();

        //Liveness checks are used to determine if the app is still running
        //Only health checks tagged with the "live" tag must pass for app to be considered alive
        var livenessHealthCheckOptions = new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(HealthChecksTags.Live)
        };

        if (settings.ResponseWriter is not null)
        {
            readinessHealthCheckOptions.ResponseWriter = settings.ResponseWriter;
            livenessHealthCheckOptions.ResponseWriter = settings.ResponseWriter;
        }

        app.MapHealthChecks(settings.ReadinessEndpointPath, readinessHealthCheckOptions);
        app.MapHealthChecks(settings.LivenessEndpointPath, livenessHealthCheckOptions);
    }
}