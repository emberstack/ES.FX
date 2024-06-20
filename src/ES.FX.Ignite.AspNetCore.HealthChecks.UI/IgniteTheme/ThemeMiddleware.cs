using ES.FX.Reflection;
using Microsoft.AspNetCore.Http;

namespace ES.FX.Ignite.AspNetCore.HealthChecks.UI.IgniteTheme;

/// <summary>
///     Middleware used to override the default theme with the Ignite theme using injected resources.
///     This will be removed once HealthChecks UI supports custom themes from streams.
/// </summary>
internal class ThemeMiddleware(RequestDelegate next)
{
    internal const string IgniteThemeResourcesPath = "ignite-theme/resources";


    public async Task InvokeAsync(HttpContext context)
    {
        // Override the default theme with the Ignite theme using injected resources
        // TODO: Remove this once HealthChecks UI supports custom themes from streams
        if (context.Request.Path.ToString().EndsWith($"{IgniteThemeResourcesPath}/healthchecksui-min.css",
                StringComparison.OrdinalIgnoreCase))
        {
            var contentStream = typeof(ThemeMiddleware).Assembly.GetManifestResources()
                .FirstOrDefault(s => s.Name.EndsWith("theme.css"))?.GetStream();
            if (contentStream is null)
            {
                context.Response.StatusCode = 404;
                return;
            }

            context.Response.ContentType = "text/css";
            await contentStream.CopyToAsync(context.Response.Body);
            return;
        }

        //TODO: Remove this once https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks/issues/2130 is resolved
        if (context.Request.Path.ToString().EndsWith($"{IgniteThemeResourcesPath}/1ae4e3706fe3f478fcc1.woff2",
                StringComparison.OrdinalIgnoreCase))
        {
            var contentStream = typeof(ThemeMiddleware).Assembly.GetManifestResources()
                .FirstOrDefault(s => s.Name.EndsWith("material.woff2"))?.GetStream();
            if (contentStream is null)
            {
                context.Response.StatusCode = 404;
                return;
            }

            context.Response.ContentType = "font/woff2";
            await contentStream.CopyToAsync(context.Response.Body);
            return;
        }

        await next(context);
    }
}