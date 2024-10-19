using Testcontainers.MsSql;

namespace ES.FX.Shared.SqlServer.Tests.Fixtures;

public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    public const string Registry = "mcr.microsoft.com";
    public const string Image = "mssql/server";
    public const string Tag = "2022-latest";
    public MsSqlContainer? Container { get; private set; }

    public async Task InitializeAsync()
    {
        Container = new MsSqlBuilder()
            .WithName($"{nameof(SqlServerContainerFixture)}-{Guid.CreateVersion7()}")
            .WithImage($"{Registry}/{Image}:{Tag}")
            .Build();
        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (Container is not null) await Container.DisposeAsync();
    }

    public string GetConnectionString() =>
        Container?.GetConnectionString() ??
        throw new InvalidOperationException("The test container was not initialized.");
}