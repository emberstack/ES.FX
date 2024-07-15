using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Asp.Versioning.Hosting;

[PublicAPI]
public static class ApiVersioningHostingExtensions
{
    /// <summary>
    ///     Registers <see cref="Versioning" /> services in the services provided by the
    ///     <paramref name="builder" />.
    ///     Enables AutoValidation for Endpoints and MVC.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="configureApiVersioningOptions">
    ///     An optional delegate that can be used for customizing settings. It's invoked after the
    ///     settings are read from the configuration.
    /// </param>
    /// <param name="configureApiExplorerOptions">
    ///     An optional delegate that can be used for customizing settings. It's invoked after the
    ///     settings are read from the configuration.
    /// </param>
    public static void IgniteApiVersioning(this IHostApplicationBuilder builder,
        Action<ApiVersioningOptions>? configureApiVersioningOptions = null,
        Action<ApiExplorerOptions>? configureApiExplorerOptions = null)
    {
        builder.Services.AddApiVersioning(options =>
            {
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();

                configureApiVersioningOptions?.Invoke(options);
            })
            .AddApiExplorer(options =>
            {
                // ReSharper disable once StringLiteralTypo
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;

                configureApiExplorerOptions?.Invoke(options);
            }).EnableApiVersionBinding();
    }
}