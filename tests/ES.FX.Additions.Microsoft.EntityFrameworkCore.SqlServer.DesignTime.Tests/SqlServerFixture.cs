using Testcontainers.MsSql;

namespace ES.FX.Additions.Microsoft.EntityFrameworkCore.SqlServer.DesignTime.Tests;

/// <summary>
///     A shared SQL Server container used by the self-configuring (<c>OnConfiguring</c>) context test.
///     The <see cref="TestContainerDesignTimeFactory{TDbContext}" /> never hands a connection string to a
///     parameterless context, so the test publishes this fixture's connection string via
///     <see cref="ParameterlessDbContext.AmbientConnectionString" /> to give the returned context a reachable DB.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;

    public string ConnectionString =>
        _container?.GetConnectionString() ??
        throw new InvalidOperationException("The fixture container was not initialized.");

    public async ValueTask InitializeAsync()
    {
        // Mirror the image coordinates the library exposes so the fixture stays aligned with the factory.
        _container = new MsSqlBuilder(
                $"{TestContainerDesignTimeFactory<ParameterlessDbContext>.Registry}/" +
                $"{TestContainerDesignTimeFactory<ParameterlessDbContext>.Image}:" +
                $"{TestContainerDesignTimeFactory<ParameterlessDbContext>.Tag}")
            .WithName($"{nameof(SqlServerFixture)}-{Guid.CreateVersion7()}")
            .Build();
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }
}
