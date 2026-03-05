using ES.FX.TransactionalOutbox.Delivery;
using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using Xunit;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.PostgreSql.Tests;

public class SimpleFunctionalTests : IAsyncLifetime
{
    private string? _connectionString;
    private PostgreSqlContainer? _postgreSqlContainer;

    public async ValueTask InitializeAsync()
    {
        // Create a dedicated PostgreSQL container for this test
        _postgreSqlContainer = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        await _postgreSqlContainer.StartAsync(TestContext.Current.CancellationToken);
        _connectionString = _postgreSqlContainer.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_postgreSqlContainer != null) await _postgreSqlContainer.DisposeAsync();
    }

    private OutboxTestDbContext CreateContext()
    {
        if (_connectionString == null)
            throw new InvalidOperationException("Container not initialized. Test setup failed.");

        var builder = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseNpgsql(_connectionString,
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
            OrderNumber = $"PG-TEST-{testId}",
            Amount = 100.50m,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        context.Orders.Add(order);
        context.AddOutboxMessage(order);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var savedOrder = await context.Orders.FirstOrDefaultAsync(o => o.OrderNumber == $"PG-TEST-{testId}", TestContext.Current.CancellationToken);
        Assert.NotNull(savedOrder);
        Assert.Equal($"PG-TEST-{testId}", savedOrder.OrderNumber);

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

    [Fact]
    public Task UsePostgreSqlOutboxProvider_Should_Configure_Provider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<OutboxTestDbContext>(options =>
        {
            options.UseNpgsql(_connectionString!);
            options.UseOutbox();
        });

        services.AddOutboxDeliveryService<OutboxTestDbContext, TestMessageHandler>(options =>
        {
            options.UsePostgreSqlOutboxProvider();
        });

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var deliveryOptions =
            serviceProvider.GetRequiredService<IOptions<OutboxDeliveryOptions<OutboxTestDbContext>>>();

        // Assert
        Assert.NotNull(deliveryOptions.Value.OutboxProvider);
        Assert.IsType<PostgreSqlOutboxProvider<OutboxTestDbContext>>(deliveryOptions.Value.OutboxProvider);

        return Task.CompletedTask;
    }

    private class TestMessageHandler : IOutboxMessageHandler
    {
        public ValueTask Handle(OutboxMessageContext context,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<bool> IsReady(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);
    }
}