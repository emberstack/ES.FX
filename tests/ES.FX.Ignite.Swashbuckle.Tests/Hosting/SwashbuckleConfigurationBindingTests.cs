using ES.FX.Ignite.Swashbuckle.Configuration;
using ES.FX.Ignite.Swashbuckle.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Swashbuckle.Tests.Hosting;

public class SwashbuckleConfigurationBindingTests
{
    [Fact]
    public void BindsSettings_FromDefaultConfigurationSection()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Default section path is "Ignite:Swashbuckle"; SparkConfig binds "<path>:Settings".
            ["Ignite:Swashbuckle:Settings:SwaggerEnabled"] = "false",
            ["Ignite:Swashbuckle:Settings:SwaggerUIEnabled"] = "false"
        });

        builder.IgniteSwashbuckle();

        var app = builder.Build();
        var settings = app.Services.GetRequiredService<SwashbuckleSparkSettings>();

        Assert.False(settings.SwaggerEnabled);
        Assert.False(settings.SwaggerUIEnabled);
    }

    [Fact]
    public void BindsSettings_FromCustomConfigurationSectionPath()
    {
        const string customSection = "Custom:Swashbuckle:Path";

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{customSection}:Settings:SwaggerEnabled"] = "false",
            [$"{customSection}:Settings:SwaggerUIEnabled"] = "true"
        });

        builder.IgniteSwashbuckle(configurationSectionPath: customSection);

        var app = builder.Build();
        var settings = app.Services.GetRequiredService<SwashbuckleSparkSettings>();

        Assert.False(settings.SwaggerEnabled);
        Assert.True(settings.SwaggerUIEnabled);
    }

    [Fact]
    public void ConfigureSettingsDelegate_OverridesBoundConfiguration()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ignite:Swashbuckle:Settings:SwaggerEnabled"] = "false"
        });

        // Config binds SwaggerEnabled=false first; the delegate runs afterwards and flips it back to true.
        builder.IgniteSwashbuckle(settings =>
        {
            Assert.False(settings.SwaggerEnabled);
            settings.SwaggerEnabled = true;
        });

        var app = builder.Build();
        var settings = app.Services.GetRequiredService<SwashbuckleSparkSettings>();

        Assert.True(settings.SwaggerEnabled);
    }

    [Fact]
    public void DefaultSettings_AreEnabled_WhenNoConfigurationProvided()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.IgniteSwashbuckle();

        var app = builder.Build();
        var settings = app.Services.GetRequiredService<SwashbuckleSparkSettings>();

        Assert.True(settings.SwaggerEnabled);
        Assert.True(settings.SwaggerUIEnabled);
    }
}
