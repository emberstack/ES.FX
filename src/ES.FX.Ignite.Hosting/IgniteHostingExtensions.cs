using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Hosting;

[PublicAPI]
public static class IgniteHostingExtensions
{
    public static void AddIgnite(this IHostApplicationBuilder builder)
    {
        AddDefaultConfigurationSlim(builder);
    }

    public static IHost UseIgnite(this IHost app)
    {
        return app;
    }


    private static void AddDefaultConfigurationSlim(IHostApplicationBuilder builder)
    {
        // Add a new environment-specific configuration file to override the default settings.
        // This allows adding environment-specific settings without changing the default settings.
        // Useful when mounting a configuration file from a secret manager or a configuration provider.
        builder.Configuration
            .AddJsonFile($"appsettings.{builder.Environment}.overrides.json",
                optional: true, reloadOnChange: true);
    }

}