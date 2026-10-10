using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace ES.FX.Shared.SqlServer.Tests.Fixtures;

/// <summary>
///     One SQL Server container per test assembly. Register it with
///     <c>[assembly: AssemblyFixture(typeof(SqlServerContainerFixture))]</c> and inject it into test class
///     constructors. Never call <see cref="InitializeAsync" /> from a test: every call starts another container.
///     Tests that need a pristine database (e.g. no migrations applied) use <see cref="CreateDatabaseAsync" />.
/// </summary>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    public const string Registry = "mcr.microsoft.com";
    public const string Image = "mssql/server";
    public const string Tag = "2025-latest";
    public MsSqlContainer? Container { get; private set; }

    public async ValueTask DisposeAsync()
    {
        if (Container is not null) await Container.DisposeAsync();
    }

    public async ValueTask InitializeAsync()
    {
        Container = new MsSqlBuilder($"{Registry}/{Image}:{Tag}")
            .WithName($"{nameof(SqlServerContainerFixture)}-{Guid.CreateVersion7()}")
            .Build();
        await Container.StartAsync();
    }

    public string GetConnectionString() =>
        Container?.GetConnectionString() ??
        throw new InvalidOperationException("The test container was not initialized.");

    /// <summary>
    ///     Creates a new, empty database on the shared server and returns a connection string targeting it, giving
    ///     the calling test an isolated database without the cost of another container.
    /// </summary>
    public async Task<string> CreateDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var databaseName = $"test_{Guid.CreateVersion7():N}";

        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new SqlConnectionStringBuilder(GetConnectionString()) { InitialCatalog = databaseName }
            .ConnectionString;
    }
}