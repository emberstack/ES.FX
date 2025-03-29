using System.Text.Json.Serialization;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using ES.FX.Extensions.Microsoft.AspNetCore.Middleware;
using ES.FX.Ignite.Configuration;
using ES.FX.Ignite.Configuration.AspNetCore;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.HealthChecks;
using HealthChecks.ApplicationStatus.DependencyInjection;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ES.FX.Ignite.Hosting;

[PublicAPI]
public static class IgniteHostingExtensions
{
    private static void AddAspNetServices(IHostApplicationBuilder builder, AspNetCoreSettings settings)
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

        if (settings.AddEndpointsApiExplorer)
            builder.Services.AddEndpointsApiExplorer();


        if (settings.AddProblemDetails)
            builder.Services.AddProblemDetails();


        if (settings.JsonStringEnumConverterEnabled)
        {
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.Converters.Add(
                    new JsonStringEnumConverter());
            });
            builder.Services.Configure<JsonOptions>(options =>
            {
                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter());
            });
        }
    }

    private static void AddHealthChecks(IHostApplicationBuilder builder, IgniteSettings settings)
    {
        var healthCheckKey = builder.Environment.ApplicationName;
        var healthCheckBuilder = builder.Services.AddHealthChecks();
        if (settings.AspNetCore.HealthChecks.ApplicationStatusCheckEnabled)
            healthCheckBuilder.AddApplicationStatus(healthCheckKey, tags: [HealthChecksTags.Live, nameof(Host)]);
    }

    private static void AddHttpClient(IHostApplicationBuilder builder, HttpClientSettings settings)
    {
        if (settings.StandardResilienceHandlerEnabled)
            builder.Services.ConfigureHttpClientDefaults(http =>
            {
                // Turn on resilience by default
                http.AddStandardResilienceHandler();
            });

        builder.Services.AddHttpClient();
    }

    private static void AddOpenTelemetry(IHostApplicationBuilder builder, IgniteSettings settings)
    {
        if (!settings.OpenTelemetry.Enabled) return;

        if (settings.OpenTelemetry.LoggingEnabled)
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = settings.OpenTelemetry.LoggingIncludeFormattedMessage;
                logging.IncludeScopes = settings.OpenTelemetry.LoggingIncludeScopes;
            });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                if (settings.Runtime.Metrics.Enabled) metrics.AddRuntimeInstrumentation();
                if (settings.HttpClient.Metrics.Enabled) metrics.AddHttpClientInstrumentation();
                if (settings.AspNetCore.Metrics.Enabled) metrics.AddAspNetCoreInstrumentation();
            })
            .WithTracing(tracing =>
            {
                if (settings.HttpClient.Tracing.Enabled) tracing.AddHttpClientInstrumentation();
                if (settings.AspNetCore.Tracing.Enabled) tracing.AddAspNetCoreInstrumentation();
            });


        if (settings.OpenTelemetry.UseOtlpExporter) builder.Services.AddOpenTelemetry().UseOtlpExporter();
        if (settings.OpenTelemetry.UseAzureMonitor) builder.Services.AddOpenTelemetry().UseAzureMonitor();

        //This must be called after AzureMonitor so attributes don't get overridden
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r =>
            {
                r.AddService(builder.Environment.ApplicationName);
                r.AddEnvironmentVariableDetector();
            });

        // Configure default options for AspNetCore tracing based on settings.
        // This is added here because the instrumentation can be added multiple times (ex: default and AzureMonitor can add the instrumentation)
        if (settings.AspNetCore.HealthChecks.Enabled && settings.AspNetCore.Tracing.HealthChecksFiltered)
            builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(Options.DefaultName, o =>
            {
                o.Filter = context =>
                    !context.Request.Path.StartsWithSegments(settings.AspNetCore.HealthChecks.LivenessEndpointPath) &&
                    !context.Request.Path.StartsWithSegments(settings.AspNetCore.HealthChecks.ReadinessEndpointPath);
            });
    }

    public static void Ignite(this IHostApplicationBuilder builder,
        Action<IgniteSettings>? configureSettings = null,
        string configurationSectionPath = IgniteConfigurationSections.Ignite)
    {
        builder.GuardConfigurationKey(nameof(FX.Ignite));

        var settings = SparkConfig.GetSettings(builder.Configuration, configurationSectionPath, configureSettings);
        builder.Services.AddSingleton(settings);

        AddOpenTelemetry(builder, settings);

        AddHealthChecks(builder, settings);

        AddHttpClient(builder, settings.HttpClient);

        AddAspNetServices(builder, settings.AspNetCore);
    }


    public static IHost Ignite(this IHost host)
    {
        var settings = host.Services.GetRequiredService<IgniteSettings>();

        if (host is WebApplication app)
        {
            if (settings.AspNetCore.UseExceptionHandler)
                app.UseExceptionHandler();

            if (settings.AspNetCore.UseStatusCodePages)
                app.UseStatusCodePages();

            if (settings.AspNetCore.UseDeveloperExceptionPage && app.Environment.IsDevelopment())
                app.UseDeveloperExceptionPage();

            UseStandardMiddleware(app, settings.AspNetCore);
            UseForwardedHeaders(app, settings.AspNetCore);
            UseHealthChecks(app, settings);
        }

        return host;
    }

    private static void UseForwardedHeaders(WebApplication app, AspNetCoreSettings settings)
    {
        if (!settings.ForwardedHeadersEnabled) return;

        var forwardingOptions = new ForwardedHeadersOptions
        {
            RequireHeaderSymmetry = false,
            ForwardedHeaders = ForwardedHeaders.All,
            ForwardLimit = null
        };
        forwardingOptions.KnownNetworks.Clear();
        forwardingOptions.KnownProxies.Clear();
        app.UseForwardedHeaders(forwardingOptions);
    }

    private static void UseHealthChecks(WebApplication app, IgniteSettings settings)
    {
        if (!settings.AspNetCore.HealthChecks.Enabled) return;

        //Readiness checks are used to determine if the app is ready to accept traffic after starting
        //All health checks must pass for app to be considered ready
        var readinessHealthCheckOptions = new HealthCheckOptions();

        //Liveness checks are used to determine if the app is still running
        //Only health checks tagged with the "live" tag must pass for app to be considered alive
        var livenessHealthCheckOptions = new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(HealthChecksTags.Live)
        };

        if (settings.AspNetCore.HealthChecks.ResponseWriter is not null)
        {
            readinessHealthCheckOptions.ResponseWriter = settings.AspNetCore.HealthChecks.ResponseWriter;
            livenessHealthCheckOptions.ResponseWriter = settings.AspNetCore.HealthChecks.ResponseWriter;
        }

        var readinessEndpointBuilder = app.MapHealthChecks(
            settings.AspNetCore.HealthChecks.ReadinessEndpointPath, readinessHealthCheckOptions);
        if (settings.AspNetCore.Metrics.HealthChecksFiltered) readinessEndpointBuilder.DisableHttpMetrics();

        var livenessEndpointBuilder = app.MapHealthChecks(
            settings.AspNetCore.HealthChecks.LivenessEndpointPath, livenessHealthCheckOptions);
        if (settings.AspNetCore.Metrics.HealthChecksFiltered) livenessEndpointBuilder.DisableHttpMetrics();
    }

    private static void UseStandardMiddleware(WebApplication app, AspNetCoreSettings settings)
    {
        if (settings.UseServerTimingMiddleware)
            app.UseMiddleware<ServerTimingMiddleware>();

        if (settings.UseQueryStringToHeaderMiddleware)
            app.UseMiddleware<QueryStringToHeaderMiddleware>();

        if (settings.UseTraceIdResponseHeader)
            app.UseMiddleware<TraceIdResponseHeaderMiddleware>();
    }
}