using Testcontainers.MariaDb;
using Xunit;

namespace ES.FX.Shared.MySql.Tests.Fixtures;

public class MySqlContainerFixture : IAsyncLifetime
{
    // Using MariaDB container as it's fully MySQL-compatible
    private readonly MariaDbContainer _mariaDbContainer = new MariaDbBuilder()
        .WithImage("mariadb:latest")
        .Build();

    public string ConnectionString => _mariaDbContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _mariaDbContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _mariaDbContainer.DisposeAsync();
    }
}