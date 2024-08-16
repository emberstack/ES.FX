using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.StackExchange.Redis.Configuration;
using ES.FX.Ignite.StackExchange.Redis.Hosting;
using ES.FX.Shared.StackExchange.Redis.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace ES.FX.Ignite.StackExchange.Redis.Tests;

public class FunctionalTests(RedisContainerFixture redisFixture)
    : IClassFixture<RedisContainerFixture>
{
    [Theory]
    [InlineData("my-key", "my-value")]
    public async Task CanConnect(string key, string value)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.IgniteRedisClient("database", configureOptions: ConfigureOptions);

        var app = builder.Build();

        var connection = app.Services.GetRequiredService<IConnectionMultiplexer>();
        Assert.NotNull(connection);

        var database = connection.GetDatabase();
        await database.StringSetAsync(key, value);

        var actualValue = await database.StringGetAsync(key);
        Assert.Equal(actualValue, value);

        return;

        void ConfigureOptions(RedisSparkOptions options)
        {
            options.ConnectionString = redisFixture.GetConnectionString();
        }
    }

    [Theory]
    //Defaults
    [InlineData(null)]
    //Keyed
    [InlineData("keyed")]
    public void CanAdd_Keyed(string? serviceKey)
    {
        const string secondServiceKey = "client2";

        var builder = Host.CreateEmptyApplicationBuilder(null);

        //Configure settings
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{RedisSpark.ConfigurationSectionPath}:database1:{SparkConfig.Settings}:{nameof(RedisSparkSettings.TracingEnabled)}",
                true.ToString()),
                    new KeyValuePair<string, string?>(
                $"{RedisSpark.ConfigurationSectionPath}:database2:{SparkConfig.Settings}:{nameof(RedisSparkSettings.TracingEnabled)}",
                true.ToString())
        ]);

        builder.IgniteRedisClient("database1", serviceKey, configureOptions: ConfigureOptions);
        builder.IgniteRedisClient("database2", secondServiceKey, configureOptions: ConfigureOptions);

        var app = builder.Build();

        var connection1 = app.Services.GetRequiredKeyedService<IConnectionMultiplexer>(serviceKey);
        var connection2 = app.Services.GetRequiredKeyedService<IConnectionMultiplexer>(secondServiceKey);

        Assert.NotSame(connection1, connection2);
        Assert.Same(connection1.Configuration.ToString(), connection2.Configuration.ToString());

        void ConfigureOptions(RedisSparkOptions options)
        {
            options.ConnectionString = redisFixture.GetConnectionString();
        }
    }
}