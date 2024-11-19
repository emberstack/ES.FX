using Testcontainers.Redis;

namespace ES.FX.Ignite.StackExchange.Redis.Tests.Fixtures;

public sealed class RedisContainerFixture : IAsyncLifetime
{
    public const string Image = "redis";
    public const string Tag = "latest";
    public RedisContainer? Container { get; private set; }

    public async Task DisposeAsync()
    {
        if (Container is not null) await Container.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        Container = new RedisBuilder()
            .WithName($"{nameof(RedisContainerFixture)}-{Guid.CreateVersion7()}")
            .WithImage($"{Image}:{Tag}")
            .Build();
        await Container.StartAsync();
    }

    public string GetConnectionString() =>
        Container?.GetConnectionString() ??
        throw new InvalidOperationException("The test container was not initialized.");
}