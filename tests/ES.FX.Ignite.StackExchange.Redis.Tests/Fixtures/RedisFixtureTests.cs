using StackExchange.Redis;

namespace ES.FX.Ignite.StackExchange.Redis.Tests.Fixtures;

public class RedisFixtureTests(RedisContainerFixture redisContainerFixture)
    : IClassFixture<RedisContainerFixture>
{
    [Theory]
    [InlineData("my-key", "my-value")]
    public async Task RedisContainer_CanConnect(string key, string value)
    {
        Assert.NotNull(redisContainerFixture.Container);
        var _connectionMultiplexer =
            await ConnectionMultiplexer.ConnectAsync(redisContainerFixture.Container.GetConnectionString());

        var database = _connectionMultiplexer.GetDatabase();
        await database.StringSetAsync(key, value);

        var actualValue = await database.StringGetAsync(key);
        Assert.Equal(actualValue, value);
    }

    [Fact]
    public async Task RedisContainer_CanExecuteScript()
    {
        const string script = @"
        -- Lua script
        for i = 1, 5, 1 do
          redis.call('incr', 'my-counter')
        end
        local mycounter = redis.call('get', 'my-counter')
        return mycounter
      ";

        Assert.NotNull(redisContainerFixture.Container);
        var result = await redisContainerFixture.Container.ExecScriptAsync(script);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("5", result.Stdout);
    }
}