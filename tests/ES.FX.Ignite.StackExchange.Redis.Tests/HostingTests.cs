using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Exceptions;
using ES.FX.Ignite.StackExchange.Redis.Configuration;
using ES.FX.Ignite.StackExchange.Redis.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.StackExchangeRedis;

namespace ES.FX.Ignite.StackExchange.Redis.Tests;

public class HostingTests
{
    [Fact]
    public void CanOverride_Options()
    {
        const string name = "database";
        var initialConnectionString = "InitialConnectionString";
        var changedConnectionString = "ChangedConnectionString";
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{RedisSpark.ConfigurationSectionPath}:{name}:{nameof(RedisSparkOptions.ConnectionString)}",
                initialConnectionString)
        ]);

        builder.IgniteRedisClient(name, configureOptions: ConfigureOptions);

        var app = builder.Build();

        var resolvedOptions = app.Services.GetRequiredService<IOptions<RedisSparkOptions>>();
        Assert.Equal(changedConnectionString, resolvedOptions.Value.ConnectionString);

        return;

        void ConfigureOptions(RedisSparkOptions options)
        {
            //Options should have correct value from configuration
            Assert.Equal(initialConnectionString, options.ConnectionString);

            //Change the options
            options.ConnectionString = changedConnectionString;
        }
    }

    [Fact]
    public void CanOverride_Settings()
    {
        var name = "database1";
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{RedisSpark.ConfigurationSectionPath}:{name}:{SparkConfig.Settings}:{nameof(RedisSparkSettings.Tracing)}:{nameof(RedisSparkSettings.Tracing.Enabled)}",
                true.ToString())
        ]);

        builder.IgniteRedisClient(name, null, ConfigureSettings);

        var app = builder.Build();

        var resolvedSettings = app.Services.GetRequiredService<RedisSparkSettings>();
        Assert.NotNull(resolvedSettings.Tracing);

        return;

        static void ConfigureSettings(RedisSparkSettings settings)
        {
            //Settings should have correct value from configuration
            Assert.True(settings.Tracing.Enabled);

            //Change the settings
            settings.Tracing.Enabled = false;
        }
    }

    [Fact]
    public void IgniteDoesNotAllowReconfiguration()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.IgniteRedisClient();

        Assert.Throws<ReconfigurationNotSupportedException>(() => { builder.IgniteRedisClient(); });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IgniteShouldAddTheServicesHealthChecks(bool tracingEnabled)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{RedisSpark.ConfigurationSectionPath}:{SparkConfig.Settings}:{nameof(RedisSparkSettings.HealthChecks)}:{nameof(RedisSparkSettings.HealthChecks.Enabled)}",
                tracingEnabled.ToString())
        ]);

        builder.IgniteRedisClient();

        var serviceProvider = builder.Build().Services;
        Assert.NotNull(serviceProvider.GetRequiredService<RedisSparkSettings>());
        Assert.Equal(serviceProvider.GetService(typeof(HealthCheckService)) != null, tracingEnabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IgniteShouldAddTheServicesTracing(bool tracingEnabled)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{RedisSpark.ConfigurationSectionPath}:{SparkConfig.Settings}:{nameof(RedisSparkSettings.Tracing)}:{nameof(RedisSparkSettings.Tracing.Enabled)}",
                tracingEnabled.ToString())
        ]);

        builder.IgniteRedisClient();

        var serviceProvider = builder.Build().Services;
        Assert.NotNull(serviceProvider.GetRequiredService<RedisSparkSettings>());
        Assert.Equal(serviceProvider.GetService(typeof(StackExchangeRedisInstrumentation)) != null, tracingEnabled);
    }
}