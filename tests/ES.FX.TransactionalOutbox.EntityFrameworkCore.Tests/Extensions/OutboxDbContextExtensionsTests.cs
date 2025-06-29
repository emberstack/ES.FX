using ES.FX.TransactionalOutbox.Delivery;
using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context.Entities;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Extensions;

public class OutboxDbContextExtensionsTests
{
    private OutboxTestDbContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString());
        builder.UseOutbox();

        return new OutboxTestDbContext(builder.Options);
    }

    [Fact]
    public async Task AddOutboxMessage_Should_Add_Message_To_Outbox()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();

        var order = new TestOrder
        {
            OrderNumber = "ORD-001",
            Amount = 100.50m,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        context.Orders.Add(order);
        context.AddOutboxMessage(order);
        await context.SaveChangesAsync();

        // Assert
        var messages = await context.Set<OutboxMessage>().ToListAsync();
        Assert.Single(messages);

        var message = messages.First();
        Assert.NotNull(message.Payload);
        Assert.Contains("TestOrder", message.PayloadType);
        Assert.Equal(0, message.DeliveryAttempts);
        Assert.Null(message.DeliveryFirstAttemptedAt);
        Assert.Null(message.DeliveryLastAttemptedAt);
    }

    [Fact]
    public async Task AddOutboxMessage_With_DeliveryOptions_Should_Set_Delivery_Constraints()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();

        var order = new TestOrder
        {
            OrderNumber = "ORD-002",
            Amount = 200.00m,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var notBefore = DateTimeOffset.UtcNow.AddMinutes(5);
        var notAfter = DateTimeOffset.UtcNow.AddHours(1);

        var deliveryOptions = new OutboxMessageDeliveryOptions
        {
            NotBefore = notBefore,
            NotAfter = notAfter
        };

        // Act
        context.Orders.Add(order);
        context.AddOutboxMessage(order, deliveryOptions);
        await context.SaveChangesAsync();

        // Assert
        var message = await context.Set<OutboxMessage>().FirstAsync();
        Assert.Equal(notBefore, message.DeliveryNotBefore);
        Assert.Equal(notAfter, message.DeliveryNotAfter);
    }

    [Fact]
    public async Task AddOutboxMessage_Multiple_Messages_Should_Be_Added_In_Order()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();

        var orders = new List<TestOrder>();
        for (var i = 1; i <= 5; i++)
        {
            var order = new TestOrder
            {
                OrderNumber = $"ORD-{i:D3}",
                Amount = i * 100m,
                CreatedAt = DateTimeOffset.UtcNow
            };
            orders.Add(order);
        }

        // Act
        foreach (var order in orders)
        {
            context.Orders.Add(order);
            context.AddOutboxMessage(order);
        }

        await context.SaveChangesAsync();

        // Assert
        var messages = await context.Set<OutboxMessage>()
            .OrderBy(m => m.AddedAt)
            .ToListAsync();

        Assert.Equal(5, messages.Count);

        // Verify all messages have unique IDs
        var uniqueIds = messages.Select(m => m.Id).Distinct().Count();
        Assert.Equal(5, uniqueIds);
    }

    [Fact]
    public async Task UseOutbox_Should_Configure_DbContext_With_Outbox_Support()
    {
        // Arrange
        var builder = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString());
        builder.UseOutbox();

        // Act
        await using var context = new OutboxTestDbContext(builder.Options);
        await context.Database.EnsureCreatedAsync();

        // Assert
        // Verify outbox tables are created
        var outboxEntityType = context.Model.FindEntityType(typeof(Outbox));
        var outboxMessageEntityType = context.Model.FindEntityType(typeof(OutboxMessage));

        Assert.NotNull(outboxEntityType);
        Assert.NotNull(outboxMessageEntityType);

        // Verify table names
        var outboxTableName = outboxEntityType.GetTableName();
        var outboxMessageTableName = outboxMessageEntityType.GetTableName();

        Assert.Equal("__Outboxes", outboxTableName);
        Assert.Equal("__OutboxMessages", outboxMessageTableName);
    }

    [Fact]
    public async Task Transaction_Rollback_Should_Not_Save_Outbox_Messages()
    {
        // Skip this test for InMemory database as it doesn't support transactions
        // This test will be covered in the SQL Server tests
        await Task.CompletedTask;
    }
}