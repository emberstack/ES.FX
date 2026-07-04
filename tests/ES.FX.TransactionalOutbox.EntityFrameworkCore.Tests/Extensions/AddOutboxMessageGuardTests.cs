using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context.Entities;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Extensions;

/// <summary>
///     Confirms the guard clauses of <see cref="DbContextExtensions.AddOutboxMessage{TMessage}" />.
/// </summary>
public class AddOutboxMessageGuardTests
{
    private static OutboxTestDbContext CreateContextWithoutOutbox()
    {
        // Deliberately do NOT call UseOutbox() so the outbox extension is missing.
        var builder = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString());
        return new OutboxTestDbContext(builder.Options);
    }

    private static OutboxTestDbContext CreateContextWithOutbox()
    {
        var builder = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString());
        builder.UseOutbox();
        return new OutboxTestDbContext(builder.Options);
    }

    [Fact]
    public void AddOutboxMessage_When_Outbox_Not_Configured_Throws_InvalidOperationException()
    {
        // Arrange
        using var context = CreateContextWithoutOutbox();
        var order = new TestOrder { OrderNumber = "ORD-001", Amount = 1m };

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => context.AddOutboxMessage(order));

        // Assert - message names the DbContext and points at UseOutbox
        Assert.Contains(nameof(OutboxTestDbContext), exception.Message);
        Assert.Contains(nameof(BuilderExtensions.UseOutbox), exception.Message);
    }

    [Fact]
    public void AddOutboxMessage_When_DbContext_Is_Null_Throws_ArgumentNullException()
    {
        // Arrange
        OutboxTestDbContext? context = null;
        var order = new TestOrder { OrderNumber = "ORD-001", Amount = 1m };

        // Act / Assert
        var exception = Assert.Throws<ArgumentNullException>(() => context!.AddOutboxMessage(order));
        Assert.Equal("dbContext", exception.ParamName);
    }

    [Fact]
    public void AddOutboxMessage_When_Message_Is_Null_Throws_ArgumentNullException()
    {
        // Arrange
        using var context = CreateContextWithOutbox();
        TestOrder? order = null;

        // Act / Assert
        var exception = Assert.Throws<ArgumentNullException>(() => context.AddOutboxMessage(order!));
        Assert.Equal("message", exception.ParamName);
    }
}