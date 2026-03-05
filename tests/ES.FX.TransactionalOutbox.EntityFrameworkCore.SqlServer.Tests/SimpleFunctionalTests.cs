using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.SqlServer.Tests;

public class SimpleFunctionalTests : IAsyncLifetime
{
    private string? _connectionString;
    private MsSqlContainer? _msSqlContainer;

    public const string Registry = "mcr.microsoft.com";
    public const string Image = "mssql/server";
    public const string Tag = "2025-latest";

    public async ValueTask InitializeAsync()
    {
        // Create a dedicated SQL Server container for this test
        _msSqlContainer = new MsSqlBuilder($"{Registry}/{Image}:{Tag}")
            .WithImage("mcr.microsoft.com/mssql/server:2025-latest")
            .Build();

        await _msSqlContainer.StartAsync(TestContext.Current.CancellationToken);
        _connectionString = _msSqlContainer.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_msSqlContainer != null) await _msSqlContainer.DisposeAsync();
    }

    private OutboxTestDbContext CreateContext()
    {
        if (_connectionString == null)
            throw new InvalidOperationException("Container not initialized. Test setup failed.");

        var builder = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseSqlServer(_connectionString,
                o => o.MigrationsAssembly(typeof(SimpleFunctionalTests).Assembly.FullName));

        builder.UseOutbox();

        return new OutboxTestDbContext(builder.Options);
    }

    [Fact]
    public async Task Can_Create_Database_With_Outbox_Tables()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        // Assert
        var canConnect = await context.Database.CanConnectAsync(TestContext.Current.CancellationToken);
        Assert.True(canConnect);

        // Verify tables exist by trying to query them
        var outboxCount = await context.Set<Outbox>().CountAsync(TestContext.Current.CancellationToken);
        var outboxMessageCount = await context.Set<OutboxMessage>().CountAsync(TestContext.Current.CancellationToken);

        Assert.True(outboxCount >= 0); // Will succeed if table exists
        Assert.True(outboxMessageCount >= 0); // Will succeed if table exists
    }

    [Fact]
    public async Task Can_Add_Outbox_Message()
    {
        // Arrange
        using var context = CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var testId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var order = new TestOrder
        {
            OrderNumber = $"SQL-TEST-{testId}",
            Amount = 100.50m,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        context.Orders.Add(order);
        context.AddOutboxMessage(order);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var savedOrder = await context.Orders.FirstOrDefaultAsync(o => o.OrderNumber == $"SQL-TEST-{testId}", TestContext.Current.CancellationToken);
        Assert.NotNull(savedOrder);
        Assert.Equal($"SQL-TEST-{testId}", savedOrder.OrderNumber);

        var outboxMessage = await context.Set<OutboxMessage>().FirstAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(outboxMessage);
        Assert.NotNull(outboxMessage.Payload);
        Assert.Contains("TestOrder", outboxMessage.PayloadType);
    }

    [Fact]
    public async Task Transaction_Ensures_Atomicity()
    {
        // Arrange
        using var context = CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var testId = Guid.NewGuid().ToString("N").Substring(0, 8);

        // Act - Success case
        using (var transaction = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            var order1 = new TestOrder
            {
                OrderNumber = $"TX-{testId}-001",
                Amount = 200m,
                CreatedAt = DateTimeOffset.UtcNow
            };

            context.Orders.Add(order1);
            context.AddOutboxMessage(order1);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        // Act - Rollback case
        using (var transaction = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            var order2 = new TestOrder
            {
                OrderNumber = $"TX-{testId}-002",
                Amount = 300m,
                CreatedAt = DateTimeOffset.UtcNow
            };

            context.Orders.Add(order2);
            context.AddOutboxMessage(order2);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        // Assert
        var orders = await context.Orders.Where(o => o.OrderNumber.StartsWith($"TX-{testId}")).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(orders);
        Assert.Equal($"TX-{testId}-001", orders[0].OrderNumber);

        // Verify only one message was saved (the committed one)
        var messageCount = await context.Set<OutboxMessage>()
            .Where(m => m.Payload.Contains(testId))
            .CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, messageCount);
    }
}