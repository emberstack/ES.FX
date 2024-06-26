﻿using Azure.Monitor.OpenTelemetry.AspNetCore;
using ES.FX.AspNetCore.HealthChecks.UI.HealthChecksEndpointRegistry;
using ES.FX.Ignite.Hosting.Configuration;
using ES.FX.Ignite.Spark;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.HealthChecks;
using HealthChecks.ApplicationStatus.DependencyInjection;
using HealthChecks.UI.Client;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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
    public static void AddIgnite(this IHostApplicationBuilder builder,
        Action<IgniteSettings>? configureSettings = null,
        string configurationSectionPath = IgniteConfigurationSections.Ignite)
    {
        builder.GuardSparkConfiguration($"{nameof(Ignite)}", $"{nameof(Ignite)} already configured.");

        var settings = SparkConfig.GetSettings(builder.Configuration, configurationSectionPath, configureSettings);
        builder.Services.AddSingleton(settings);

        AddConfiguration(builder, settings.Configuration);

        AddOpenTelemetry(builder, settings.OpenTelemetry);

        AddHealthChecks(builder, settings.HealthChecks);

        AddHttpClient(builder, settings.HttpClient);
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
                if (settings.AspNetCoreMetricsEnabled) metrics.AddAspNetCoreInstrumentation();
                if (settings.HttpClientMetricsEnabled) metrics.AddHttpClientInstrumentation();
                if (settings.RuntimeMetricsEnabled) metrics.AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                if (settings.AspNetCoreTracingEnabled) tracing.AddAspNetCoreInstrumentation();
                if (settings.HttpClientTracingEnabled) tracing.AddHttpClientInstrumentation();
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


    public static IHost UseIgnite(this IHost app)
    {
        var settings = app.Services.GetRequiredService<IgniteSettings>();

        UseHealthChecks(app, settings.HealthChecks);

        return app;
    }

    private static void UseHealthChecks(IHost host, IgniteHealthChecksSettings settings)
    {
        if (host is not WebApplication app) return;
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

        if (settings.HealthChecksUiResponseWriterEnabled)
        {
            readinessHealthCheckOptions.ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse;
            livenessHealthCheckOptions.ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse;
        }

        app.MapHealthChecks(settings.ReadinessEndpointPath, readinessHealthCheckOptions);
        app.MapHealthChecks(settings.LivenessEndpointPath, livenessHealthCheckOptions);


        var uiHealthChecksRegistry = app.Services.GetService<HealthChecksEndpointRegistryService>();
        if (settings.HealthChecksEndpointsAutoRegister && uiHealthChecksRegistry is not null)
        {
            uiHealthChecksRegistry.AddHealthCheckEndpoint(HealthChecksEndpoints.ReadinessEndpointName,
                settings.ReadinessEndpointPath);
            uiHealthChecksRegistry.AddHealthCheckEndpoint(HealthChecksEndpoints.LivenessEndpointName,
                settings.LivenessEndpointPath);
        }
    }
}