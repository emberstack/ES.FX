using ES.FX.Ignite.StackExchange.Redis.Configuration;
using ES.FX.Ignite.StackExchange.Redis.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace ES.FX.Ignite.StackExchange.Redis.Tests;

/// <summary>
///     Covers the <see cref="IConnectionMultiplexer" /> factory's configuration-source selection in
///     <see cref="RedisHostingExtensions.IgniteRedisClient" />: the documented precedence where a
///     non-blank <see cref="RedisSparkOptions.ConnectionString" /> wins, the fallback to
///     <see cref="RedisSparkOptions.ConfigurationOptions" />, and the final fallback to a brand-new
///     <see cref="ConfigurationOptions" /> when both are unset.
/// </summary>
/// <remarks>
///     No live Redis server is required: every case uses <c>abortConnect=false</c> so
///     <see cref="ConnectionMultiplexer.Connect(ConfigurationOptions, TextWriter)" /> returns a
///     (disconnected) multiplexer whose configuration reflects which branch was taken.
/// </remarks>
public class ConnectionFactoryTests
{
    private static IConnectionMultiplexer Resolve(Action<RedisSparkOptions> configureOptions)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        // Tracing/health checks off keeps this focused on the connection factory branch.
        builder.IgniteRedisClient(
            configureOptions: configureOptions,
            configureSettings: settings =>
            {
                settings.Tracing.Enabled = false;
                settings.HealthChecks.Enabled = false;
            });
        var app = builder.Build();
        return app.Services.GetRequiredService<IConnectionMultiplexer>();
    }

    [Fact]
    public void ConnectionString_Takes_Precedence_Over_ConfigurationOptions()
    {
        var connection = Resolve(options =>
        {
            options.ConnectionString =
                "conn-string-host:6399,abortConnect=false,connectTimeout=250,name=FromConnectionString";

            // A completely different endpoint/name that must be ignored entirely.
            var ignored = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                ClientName = "FromConfigurationOptions"
            };
            ignored.EndPoints.Add("config-options-host", 7001);
            options.ConfigurationOptions = ignored;
        });

        // The connection string branch parsed the connection-string endpoint, not the ConfigurationOptions one.
        Assert.Contains("conn-string-host:6399", connection.Configuration);
        Assert.DoesNotContain("config-options-host", connection.Configuration);
    }

    [Fact]
    public void Falls_Back_To_ConfigurationOptions_When_ConnectionString_Blank()
    {
        var configurationOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectTimeout = 250
        };
        configurationOptions.EndPoints.Add("config-options-host", 7002);

        var connection = Resolve(options =>
        {
            options.ConnectionString = "   "; // whitespace => IsNullOrWhiteSpace true => fallback
            options.ConfigurationOptions = configurationOptions;
        });

        Assert.Contains("config-options-host:7002", connection.Configuration);
    }

    [Fact]
    public void Falls_Back_To_New_ConfigurationOptions_When_Both_Unset()
    {
        // Both ConnectionString and ConfigurationOptions are null => the factory builds a fresh
        // ConfigurationOptions() with no endpoints. StackExchange.Redis rejects a connect with no
        // endpoints, so resolution throws. This asserts the current, real behavior of that branch.
        var ex = Record.Exception(() => Resolve(options =>
        {
            options.ConnectionString = null;
            options.ConfigurationOptions = null;
        }));

        Assert.NotNull(ex);
    }
}
