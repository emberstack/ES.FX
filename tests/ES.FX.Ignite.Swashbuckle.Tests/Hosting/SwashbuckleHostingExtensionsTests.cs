using ES.FX.Ignite.Spark.Exceptions;
using ES.FX.Ignite.Swashbuckle.Configuration;
using ES.FX.Ignite.Swashbuckle.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Swashbuckle.Tests.Hosting;

public class SwashbuckleHostingExtensionsTests
{
    [Fact]
    public void IgniteDoesNotAllowReconfiguration()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.IgniteSwashbuckle();

        Assert.Throws<ReconfigurationNotSupportedException>(() => builder.IgniteSwashbuckle());
    }

    [Fact]
    public void CanOverride_Settings()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.IgniteSwashbuckle(ConfigureSettings);

        var app = builder.Build();

        var resolvedSettings = app.Services.GetRequiredService<SwashbuckleSparkSettings>();
        Assert.False(resolvedSettings.SwaggerEnabled);

        return;

        static void ConfigureSettings(SwashbuckleSparkSettings settings)
        {
            Assert.True(settings.SwaggerEnabled);

            settings.SwaggerEnabled = false;
        }
    }
}