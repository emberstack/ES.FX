using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit.Abstractions;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.SqlServer.Tests;

public class OutboxDeliveryTests : OutboxDeliveryTestsBase, IAsyncLifetime
{
    private string? _connectionString;
    private MsSqlContainer? _msSqlContainer;

    public OutboxDeliveryTests(ITestOutputHelper output) : base(output)
    {
    }

    public async Task InitializeAsync()
    {
        // Create a dedicated SQL Server container for this test class
        _msSqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();

        await _msSqlContainer.StartAsync();
        _connectionString = _msSqlContainer.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_msSqlContainer != null) await _msSqlContainer.DisposeAsync();
    }

    protected override void ConfigureDbContext(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        optionsBuilder.UseSqlServer(connectionString,
            o => o.MigrationsAssembly(typeof(OutboxDeliveryTests).Assembly.FullName));
    }

    protected override void ConfigureOutboxDelivery(OutboxDeliveryOptions<OutboxTestDbContext> options)
    {
        options.UseSqlServerOutboxProvider();
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

    protected override async Task InitializeDatabaseAsync(OutboxTestDbContext context)
    {
        // SQL Server uses migrations
        await context.Database.MigrateAsync();
    }
}