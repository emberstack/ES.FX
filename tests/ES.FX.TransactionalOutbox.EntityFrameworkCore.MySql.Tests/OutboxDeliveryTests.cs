using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MariaDb;
using Xunit;
using Xunit.Abstractions;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.MySql.Tests;

public class OutboxDeliveryTests : OutboxDeliveryTestsBase, IAsyncLifetime
{
    private string? _connectionString;
    private MariaDbContainer? _mariaDbContainer;

    public OutboxDeliveryTests(ITestOutputHelper output) : base(output)
    {
    }

    public async Task InitializeAsync()
    {
        // Create a dedicated MariaDB container for this test class
        _mariaDbContainer = new MariaDbBuilder("mariadb:latest")
            .Build();

        await _mariaDbContainer.StartAsync();
        _connectionString = _mariaDbContainer.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_mariaDbContainer != null) await _mariaDbContainer.DisposeAsync();
    }

    protected override void ConfigureDbContext(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
            o => o.MigrationsAssembly(typeof(OutboxDeliveryTests).Assembly.FullName));
    }

    protected override void ConfigureOutboxDelivery(OutboxDeliveryOptions<OutboxTestDbContext> options)
    {
        options.UseMySqlOutboxProvider();
    }

    protected override Task<string> GetConnectionStringAsync()
    {
        if (_connectionString == null)
            throw new InvalidOperationException("Container not initialized. Call InitializeAsync first.");

        return Task.FromResult(_connectionString);
    }

    protected override Task CleanupAsync(string connectionString) =>
        // No cleanup needed - container will be disposed
        Task.CompletedTask;
}