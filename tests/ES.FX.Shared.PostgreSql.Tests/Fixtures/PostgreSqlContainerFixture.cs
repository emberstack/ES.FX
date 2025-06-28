using Testcontainers.PostgreSql;
using Xunit;

namespace ES.FX.Shared.PostgreSql.Tests.Fixtures;

public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    public const string Registry = "docker.io";
    public const string Image = "postgres";
    public const string Tag = "16-alpine";
    public PostgreSqlContainer? Container { get; private set; }

    public async Task DisposeAsync()
    {
        if (Container is not null) await Container.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        Container = new PostgreSqlBuilder()
            .WithName($"{nameof(PostgreSqlContainerFixture)}-{Guid.CreateVersion7()}")
            .WithImage($"{Registry}/{Image}:{Tag}")
            .Build();
        await Container.StartAsync();
    }

    public string GetConnectionString() =>
        Container?.GetConnectionString() ??
        throw new InvalidOperationException("The test container was not initialized.");
}