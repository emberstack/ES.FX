using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ES.FX.AspNetCore.HealthChecks.UI.HealthChecksEndpointRegistry;

public static class HealthChecksEndpointRegistryServiceHostingExtensions
{
    /// <summary>
    ///     Add health checks UI registry service to the host application builder.
    /// </summary>
    /// <param name="builder"> The host application builder. </param>
    public static void AddHealthChecksEndpointRegistry(this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<HealthChecksEndpointRegistryService>();
        builder.Services.AddHostedService(
            provider => provider.GetRequiredService<HealthChecksEndpointRegistryService>());
    }
}