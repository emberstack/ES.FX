using DotNet.Testcontainers.Builders;
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
            .WithName($"{nameof(SqlServerContainerFixture)}-{Guid.NewGuid()}")
            .WithImage($"{Registry}/{Image}:{Tag}")
            // FIXME until this is fixed https://github.com/testcontainers/testcontainers-dotnet/pull/1221
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted(
                    "/opt/mssql-tools18/bin/sqlcmd",
                    "-C",
                    "-Q",
                    "SELECT 1;"
                )
            )
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