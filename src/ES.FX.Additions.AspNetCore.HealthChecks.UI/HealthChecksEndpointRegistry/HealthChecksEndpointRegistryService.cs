using System.Collections.Concurrent;
using HealthChecks.UI.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ES.FX.Additions.AspNetCore.HealthChecks.UI.HealthChecksEndpointRegistry;

/// <summary>
///     Service used to register health checks endpoints in the HealthChecksUI database after application started.
/// </summary>
/// <param name="logger"> The logger. </param>
/// <param name="serviceProvider"> Service provider used to create a scope and get the <see cref="HealthChecksDb" />. </param>
/// <param name="hostLifetime"> The host application lifetime. </param>
public class HealthChecksEndpointRegistryService(
    ILogger<HealthChecksEndpointRegistryService> logger,
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostLifetime) : BackgroundService
{
    private readonly ConcurrentBag<HealthCheckConfiguration> _healthCheckConfigurations = [];

    /// <summary>
    ///     Adds a health check configuration to the registry.
    /// </summary>
    /// <param name="configuration"> The health check configuration. </param>
    public void AddHealthCheckConfiguration(HealthCheckConfiguration configuration)
    {
        _healthCheckConfigurations.Add(configuration);
    }


    /// <summary>
    ///     Adds a health check endpoint to the registry.
    /// </summary>
    /// <param name="name"> The name of the health check. </param>
    /// <param name="uri"> The uri of the health check. </param>
    public void AddHealthCheckEndpoint(string name, string uri)
    {
        AddHealthCheckConfiguration(new HealthCheckConfiguration
        {
            Name = name,
            Uri = uri
        });
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogTrace($"Registering callback for {nameof(hostLifetime.ApplicationStarted)}");
        hostLifetime.ApplicationStarted.Register(() =>
        {
            var configurations = _healthCheckConfigurations.ToArray();
            logger.LogDebug("{count} health check configuration(s) available", configurations.Length);

            logger.LogDebug("Registering health check configurations");
            using var scope = serviceProvider.CreateScope();
            using var db = scope.ServiceProvider.GetRequiredService<HealthChecksDb>();
            db.Configurations.AddRange(configurations);
            db.SaveChanges();
        });
        return Task.CompletedTask;
    }
}