using ES.FX.Ignite.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.Ignite.OpenTelemetry.AspNetCore;

/// <summary>
///     Filter to ignore health checks requests from OpenTelemetry
/// </summary>
public static class IgnoreHealthChecksRequests
{
    /// <summary>
    ///     Gets a filter to ignore health checks requests from OpenTelemetry
    /// </summary>
    public static Func<HttpContext, bool> Filter { get; set; } = context =>
    {
        var settings = context.RequestServices.GetRequiredService<IgniteSettings>();
        if (!settings.HealthChecks.EndpointEnabled) return true;
        if (context.Request.Path.StartsWithSegments(settings.HealthChecks.LivenessEndpointPath)) return false;
        if (context.Request.Path.StartsWithSegments(settings.HealthChecks.ReadinessEndpointPath)) return false;
        return true;
    };
}