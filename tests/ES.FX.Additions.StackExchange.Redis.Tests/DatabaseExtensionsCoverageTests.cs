using ES.FX.Shared.Redis.Tests.Fixtures;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

namespace ES.FX.Additions.StackExchange.Redis.Tests;

/// <summary>
///     Additional functional coverage for <see cref="DatabaseExtensions" />. These exercise the
///     server-side Lua scripts (prefix determination, batched SCAN paging, sync/async delete paths)
///     against a real Redis instance because the advertised behavior (SCAN cursor loop, keyspace
///     isolation, prefix arithmetic) cannot be faithfully reproduced with a mock.
/// </summary>
public class DatabaseExtensionsCoverageTests(RedisContainerFixture redisFixture)
    : IClassFixture<RedisContainerFixture>
{
    // Each test owns a unique database index so keyspaces never overlap; the container is created
    // fresh per class fixture, so every index starts empty without needing admin-mode FLUSHDB.
    private async Task<IConnectionMultiplexer> ConnectAsync() =>
        await ConnectionMultiplexer.ConnectAsync(redisFixture.GetConnectionString());

    [Fact]
    public async Task GetKeyPrefixAsync_Returns_Empty_For_A_NonPrefixed_Database()
    {
        using var multiplexer = await ConnectAsync();
        var database = multiplexer.GetDatabase(3);

        var result = await database.GetKeyPrefixAsync();

        // sub("Redis", 1, len("Redis") - len("Redis")) == sub("Redis", 1, 0) == ""
        Assert.Equal(string.Empty, result.ToString());
    }

    [Fact]
    public async Task GetKeyPrefix_Returns_Empty_For_A_NonPrefixed_Database()
    {
        using var multiplexer = await ConnectAsync();
        var database = multiplexer.GetDatabase(3);

        var result = database.GetKeyPrefix();

        Assert.Equal(string.Empty, result.ToString());
    }

    [Fact]
    public async Task GetKeyPrefixAsync_Returns_The_Configured_Prefix_For_A_Prefixed_Database()
    {
        using var multiplexer = await ConnectAsync();
        var database = multiplexer.GetDatabase(3);
        var prefixed = database.WithKeyPrefix("tenant-x:");

        var result = await prefixed.GetKeyPrefixAsync();

        // KEYS[1] is prefixed to "tenant-x:Redis"; ARGV[1] stays "Redis".
        // sub("tenant-x:Redis", 1, 14 - 5) == "tenant-x:"
        Assert.Equal("tenant-x:", result.ToString());
    }

    [Fact]
    public async Task GetKeyPrefix_Returns_The_Configured_Prefix_For_A_Prefixed_Database()
    {
        using var multiplexer = await ConnectAsync();
        var database = multiplexer.GetDatabase(3);
        var prefixed = database.WithKeyPrefix("acme:");

        var result = prefixed.GetKeyPrefix();

        Assert.Equal("acme:", result.ToString());
    }

    [Fact]
    public async Task KeysDeleteAllAsync_Deletes_Every_Key_And_Returns_The_Count()
    {
        using var multiplexer = await ConnectAsync();
        var database = multiplexer.GetDatabase(4);

        await database.StringSetAsync("a", "v");
        await database.StringSetAsync("b", "v");
        await database.StringSetAsync("c", "v");

        var deleted = await database.KeysDeleteAllAsync();

        Assert.Equal(3, deleted);
        Assert.False(await database.KeyExistsAsync("a"));
        Assert.False(await database.KeyExistsAsync("b"));
        Assert.False(await database.KeyExistsAsync("c"));
    }

    [Fact]
    public async Task KeysDeleteAllAsync_Returns_Zero_On_An_Empty_Database()
    {
        using var multiplexer = await ConnectAsync();
        var database = multiplexer.GetDatabase(5);

        var deleted = await database.KeysDeleteAllAsync();

        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task KeysDelete_Sync_Deletes_Only_Keys_Matching_The_Pattern_And_Returns_The_Count()
    {
        using var multiplexer = await ConnectAsync();
        var database = multiplexer.GetDatabase(6);

        await database.StringSetAsync("session:1", "v");
        await database.StringSetAsync("session:2", "v");
        await database.StringSetAsync("session:3", "v");
        await database.StringSetAsync("other", "v");

        var deleted = database.KeysDelete("session:*");

        Assert.Equal(3, deleted);
        Assert.False(await database.KeyExistsAsync("session:1"));
        Assert.False(await database.KeyExistsAsync("session:2"));
        Assert.False(await database.KeyExistsAsync("session:3"));
        Assert.True(await database.KeyExistsAsync("other"));
    }

    [Fact]
    public async Task KeysDelete_Sync_Returns_Zero_When_No_Key_Matches_The_Pattern()
    {
        using var multiplexer = await ConnectAsync();
        var database = multiplexer.GetDatabase(7);

        await database.StringSetAsync("keep:1", "v");

        var deleted = database.KeysDelete("nomatch:*");

        Assert.Equal(0, deleted);
        Assert.True(await database.KeyExistsAsync("keep:1"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(50)]
    public async Task KeysDeleteAsync_Pages_Through_The_Whole_Keyspace_With_Small_Batch_Sizes(int batchSize)
    {
        // The batched SCAN loop only iterates more than once when there are more keys than the
        // batch size. With hundreds of keys and a tiny COUNT, the Lua repeat...until cursor=='0'
        // loop must page multiple times and still delete every matching key exactly once.
        // A dedicated database index per batch size keeps the runs isolated.
        using var multiplexer = await ConnectAsync();
        var database = multiplexer.GetDatabase(batchSize switch { 1 => 8, 7 => 11, _ => 12 });

        const int total = 250;
        for (var i = 0; i < total; i++)
            await database.StringSetAsync($"item:{i}", "v");
        // A few non-matching keys must survive.
        await database.StringSetAsync("keepme", "v");
        await database.StringSetAsync("other:1", "v");

        var deleted = await database.KeysDeleteAsync("item:*", batchSize);

        Assert.Equal(total, deleted);
        Assert.False(await database.KeyExistsAsync("item:0"));
        Assert.False(await database.KeyExistsAsync("item:249"));
        Assert.True(await database.KeyExistsAsync("keepme"));
        Assert.True(await database.KeyExistsAsync("other:1"));
    }

    [Fact]
    public async Task KeysDelete_Sync_Pages_Through_The_Whole_Keyspace_With_Batch_Size_One()
    {
        using var multiplexer = await ConnectAsync();
        var database = multiplexer.GetDatabase(9);

        const int total = 120;
        for (var i = 0; i < total; i++)
            await database.StringSetAsync($"bulk:{i}", "v");

        var deleted = database.KeysDelete("*", 1);

        Assert.Equal(total, deleted);
        Assert.False(await database.KeyExistsAsync("bulk:0"));
        Assert.False(await database.KeyExistsAsync("bulk:119"));
    }

    [Fact]
    public async Task KeysDeleteAllAsync_With_Small_Batch_Size_Pages_And_Deletes_Everything()
    {
        using var multiplexer = await ConnectAsync();
        var database = multiplexer.GetDatabase(10);

        const int total = 200;
        for (var i = 0; i < total; i++)
            await database.StringSetAsync($"k:{i}", "v");

        var deleted = await database.KeysDeleteAllAsync(5);

        Assert.Equal(total, deleted);
        Assert.False(await database.KeyExistsAsync("k:0"));
        Assert.False(await database.KeyExistsAsync("k:199"));
    }
}