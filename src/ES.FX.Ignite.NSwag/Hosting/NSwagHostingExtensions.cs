using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSwag.AspNetCore;
using SwaggerThemes;
using System.Threading.Tasks;

namespace ES.FX.Ignite.NSwag.Hosting;

[PublicAPI]
public static class NSwagHostingExtensions
{
    /// <summary>
    ///     Uses <see cref="NSwag" />
    ///     Enables the OpenAPI/Swagger specification generation and the Swagger UI.
    /// </summary>
    /// <param name="app"> The <see cref="WebApplication" /> to configure the Ignite HealthChecks UI for.</param>
    /// <param name="useSwaggerUi"> A flag indicating whether to use Swagger UI. Default is <c>true</c>.</param>
    /// <param name="useSwaggerUiDarkMode"> A flag indicating whether to use Swagger UI dark mode. Default is <c>true</c>.</param>
    /// <param name="configureOpenApiDocumentMiddlewareSettings">
    ///     An optional delegate that can be used for customizing <see cref="OpenApiDocumentMiddlewareSettings" />.
    /// </param>
    /// <param name="configureSwaggerUiSettings">
    ///     An optional delegate that can be used for customizing <see cref="SwaggerUiSettings" />.
    /// </param>
    public static void IgniteNSwag(this WebApplication app,
        bool useSwaggerUi = true,
        bool useSwaggerUiDarkMode = true,
        Action<OpenApiDocumentMiddlewareSettings>? configureOpenApiDocumentMiddlewareSettings = null,
        Action<SwaggerUiSettings>? configureSwaggerUiSettings = null)
    {
        app.UseOpenApi(configureOpenApiDocumentMiddlewareSettings);

        

        if (useSwaggerUi)
            app.UseSwaggerUi(settings =>
            {
                if (useSwaggerUiDarkMode) 
                    settings.CustomInlineStyles = SwaggerTheme.GetSwaggerThemeCss(Theme.UniversalDark);

                var env = app.Services.GetRequiredService<IHostEnvironment>();
                settings.DocumentTitle = $"{env.ApplicationName} - Swagger UI";
                settings.DocExpansion = "list";
                configureSwaggerUiSettings?.Invoke(settings);
            });
            
    }
}