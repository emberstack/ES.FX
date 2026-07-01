using ES.FX.Shared.Redis.Tests.Fixtures;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

namespace ES.FX.Additions.StackExchange.Redis.Tests;

public class DatabaseExtensionsTests(RedisContainerFixture redisFixture)
    : IClassFixture<RedisContainerFixture>
{
    private async Task<IConnectionMultiplexer> ConnectAsync() =>
        await ConnectionMultiplexer.ConnectAsync(redisFixture.GetConnectionString());

    [Fact]
    public async Task KeysDeleteAsync_Deletes_Only_Keys_Matching_The_Pattern()
    {
        using var multiplexer = await ConnectAsync();
        var database = multiplexer.GetDatabase(0);

        await database.StringSetAsync("session:1", "v");
        await database.StringSetAsync("session:2", "v");
        await database.StringSetAsync("other", "v");

        var deleted = await database.KeysDeleteAsync("session:*");

        Assert.Equal(2, deleted);
        Assert.False(await database.KeyExistsAsync("session:1"));
        Assert.False(await database.KeyExistsAsync("session:2"));
        Assert.True(await database.KeyExistsAsync("other"));
    }

    [Fact]
    public async Task KeysDeleteAsync_Respects_The_Database_Key_Prefix()
    {
        // The delete runs as a server-side script evaluated through the IDatabase, so a key-prefixed
        // (keyspace-isolated) database must only ever match and delete its own keys.
        using var multiplexer = await ConnectAsync();
        var database = multiplexer.GetDatabase(1);
        var prefixed = database.WithKeyPrefix("tenant1:");

        await database.StringSetAsync("tenant2:a", "v");
        await database.StringSetAsync("plain", "v");
        await prefixed.StringSetAsync("a", "v");
        await prefixed.StringSetAsync("b", "v");

        var deleted = await prefixed.KeysDeleteAsync("*");

        Assert.Equal(2, deleted);
        Assert.False(await database.KeyExistsAsync("tenant1:a"));
        Assert.False(await database.KeyExistsAsync("tenant1:b"));
        Assert.True(await database.KeyExistsAsync("tenant2:a"));
        Assert.True(await database.KeyExistsAsync("plain"));
    }

    [Fact]
    public async Task KeysDeleteAll_Deletes_Every_Key_On_The_Database()
    {
        using var multiplexer = await ConnectAsync();
        var database = multiplexer.GetDatabase(2);

        await database.StringSetAsync("a", "v");
        await database.StringSetAsync("b", "v");
        await database.StringSetAsync("c", "v");

        var deleted = database.KeysDeleteAll();

        Assert.Equal(3, deleted);
        Assert.False(await database.KeyExistsAsync("a"));
        Assert.False(await database.KeyExistsAsync("b"));
        Assert.False(await database.KeyExistsAsync("c"));
    }
}