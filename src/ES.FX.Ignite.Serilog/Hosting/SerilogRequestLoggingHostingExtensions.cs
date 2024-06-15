using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.AspNetCore;
using Serilog.Events;

namespace ES.FX.Ignite.Serilog.Hosting;

[PublicAPI]
public static class SerilogRequestLoggingHostingExtensions
{
    /// <summary>
    ///     Adds Serilog Request logging to the application
    /// </summary>
    /// <param name="app"> The <see cref="IApplicationBuilder" />.</param>
    /// <param name="configureOptions">
    ///     An optional delegate that can be used for customizing the
    ///     <see cref="RequestLoggingOptions" />.
    /// </param>
    public static void UseSerilogRequestLogging(this IApplicationBuilder app,
        Action<RequestLoggingOptions>? configureOptions = null)
    {
        //Add Serilog Request logging
        SerilogApplicationBuilderExtensions.UseSerilogRequestLogging(app, options =>
        {
            ConfigureDefaultRequestLoggingSlim(options);
            configureOptions?.Invoke(options);
        });
    }


    private static void ConfigureDefaultRequestLoggingSlim(RequestLoggingOptions options)
    {
        options.GetLevel = (_, _, _) => LogEventLevel.Debug;

        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        };
    }
}