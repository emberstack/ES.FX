using ES.FX.Ignite.Migrations.Configuration;
using ES.FX.Ignite.Migrations.Hosting;
using ES.FX.Ignite.Migrations.Service;
using ES.FX.Ignite.Spark.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Migrations.Tests;

public class MigrationsServiceHostingExtensionsTests
{
    private static HostApplicationBuilder CreateBuilder(IEnumerable<KeyValuePair<string, string?>>? config = null)
    {
        var builder = Host.CreateApplicationBuilder();
        if (config is not null) builder.Configuration.AddInMemoryCollection(config);
        return builder;
    }

    [Fact]
    public void IgniteMigrationsService_Defaults_WhenNoConfiguration_UsesSettingDefaults()
    {
        var builder = CreateBuilder();

        builder.IgniteMigrationsService();

        using var provider = builder.Services.BuildServiceProvider();
        var settings = provider.GetRequiredService<MigrationsServiceSparkSettings>();

        Assert.True(settings.Enabled);
        Assert.False(settings.ExitOnComplete);
    }

    [Fact]
    public void IgniteMigrationsService_BindsSettingsFromDefaultConfigurationSection()
    {
        // Default section path is Ignite:Services:MigrationsService, then SparkConfig appends ":Settings".
        var section = $"{MigrationsServiceSpark.ConfigurationSectionPath}:Settings";
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            [$"{section}:Enabled"] = "false",
            [$"{section}:ExitOnComplete"] = "true"
        });

        builder.IgniteMigrationsService();

        using var provider = builder.Services.BuildServiceProvider();
        var settings = provider.GetRequiredService<MigrationsServiceSparkSettings>();

        Assert.False(settings.Enabled);
        Assert.True(settings.ExitOnComplete);
    }

    [Fact]
    public void IgniteMigrationsService_BindsSettingsFromCustomConfigurationSectionPath()
    {
        const string customPath = "Custom:Migrations";
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            [$"{customPath}:Settings:Enabled"] = "false",
            [$"{customPath}:Settings:ExitOnComplete"] = "true",
            // Ensure the default section is NOT what got read.
            [$"{MigrationsServiceSpark.ConfigurationSectionPath}:Settings:Enabled"] = "true"
        });

        builder.IgniteMigrationsService(configurationSectionPath: customPath);

        using var provider = builder.Services.BuildServiceProvider();
        var settings = provider.GetRequiredService<MigrationsServiceSparkSettings>();

        Assert.False(settings.Enabled);
        Assert.True(settings.ExitOnComplete);
    }

    [Fact]
    public void IgniteMigrationsService_ConfigureSettingsDelegate_RunsAfterConfigurationBinding()
    {
        var section = $"{MigrationsServiceSpark.ConfigurationSectionPath}:Settings";
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            [$"{section}:Enabled"] = "true",
            [$"{section}:ExitOnComplete"] = "false"
        });

        bool? boundEnabledSeenByDelegate = null;
        builder.IgniteMigrationsService(settings =>
        {
            // The delegate observes the config-bound value, then overrides both.
            boundEnabledSeenByDelegate = settings.Enabled;
            settings.Enabled = false;
            settings.ExitOnComplete = true;
        });

        using var provider = builder.Services.BuildServiceProvider();
        var settings = provider.GetRequiredService<MigrationsServiceSparkSettings>();

        Assert.True(boundEnabledSeenByDelegate);   // config bound before delegate ran
        Assert.False(settings.Enabled);            // delegate override wins
        Assert.True(settings.ExitOnComplete);
    }

    [Fact]
    public void IgniteMigrationsService_RegistersHostedService()
    {
        var builder = CreateBuilder();

        builder.IgniteMigrationsService();

        Assert.Contains(builder.Services, d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(MigrationsService));
    }

    [Fact]
    public void IgniteMigrationsService_CalledTwice_ThrowsReconfigurationNotSupported()
    {
        var builder = CreateBuilder();

        builder.IgniteMigrationsService();

        Assert.Throws<ReconfigurationNotSupportedException>(() => builder.IgniteMigrationsService());
    }
}
