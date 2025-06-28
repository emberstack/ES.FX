using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;
using Xunit.Abstractions;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.PostgreSql.Tests;

public class OutboxDeliveryTests : OutboxDeliveryTestsBase, IAsyncLifetime
{
    private string? _connectionString;
    private PostgreSqlContainer? _postgreSqlContainer;

    public OutboxDeliveryTests(ITestOutputHelper output) : base(output)
    {
    }

    public async Task InitializeAsync()
    {
        // Create a dedicated PostgreSQL container for this test class
        _postgreSqlContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();

        await _postgreSqlContainer.StartAsync();
        _connectionString = _postgreSqlContainer.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_postgreSqlContainer != null) await _postgreSqlContainer.DisposeAsync();
    }

    protected override void ConfigureDbContext(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        optionsBuilder.UseNpgsql(connectionString,
            o => o.MigrationsAssembly(typeof(OutboxDeliveryTests).Assembly.FullName));
    }

    protected override void ConfigureOutboxDelivery(OutboxDeliveryOptions<OutboxTestDbContext> options)
    {
        options.UsePostgreSqlOutboxProvider();
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
        // PostgreSQL uses migrations
        await context.Database.MigrateAsync();
    }
}