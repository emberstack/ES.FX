using System.Globalization;
using DotNet.Testcontainers.Containers;
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

    /// <summary>
    ///     How many times <see cref="StartContainerAsync" /> boots a container before giving up.
    /// </summary>
    public const int StartAttempts = 3;

    /// <summary>
    ///     The per-instance memory cap passed as <c>MSSQL_MEMORY_LIMIT_MB</c>. Without it every instance sizes itself
    ///     to 80% of the host's memory, so several instances on one CI runner each assume they own most of it.
    /// </summary>
    public const int MemoryLimitMb = 2048;

    public MsSqlContainer? Container { get; private set; }

    public async ValueTask DisposeAsync()
    {
        if (Container is not null) await Container.DisposeAsync();
    }

    public async ValueTask InitializeAsync() =>
        Container = await StartContainerAsync($"{Registry}/{Image}:{Tag}", nameof(SqlServerContainerFixture));

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

    /// <summary>
    ///     Builds and starts a SQL Server container capped at <see cref="MemoryLimitMb" />. On a loaded CI runner the
    ///     engine can crash while booting, before it ever accepts a connection, so a crashed start is disposed and
    ///     retried on a fresh container, up to <see cref="StartAttempts" /> times.
    /// </summary>
    public static async Task<MsSqlContainer> StartContainerAsync(string image, string namePrefix,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1;; attempt++)
        {
            var container = new MsSqlBuilder(image)
                .WithName($"{namePrefix}-{Guid.CreateVersion7()}")
                .WithEnvironment("MSSQL_MEMORY_LIMIT_MB", MemoryLimitMb.ToString(CultureInfo.InvariantCulture))
                .Build();
            try
            {
                await container.StartAsync(cancellationToken);
                return container;
            }
            catch (ContainerNotRunningException)
            {
                await container.DisposeAsync();
                if (attempt >= StartAttempts) throw;

                // Back off so the load that crashed this boot has a chance to subside.
                await Task.Delay(TimeSpan.FromSeconds(5 * attempt), cancellationToken);
            }
        }
    }
}
