using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context.Entities;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Internals;

/// <summary>
///     Confirms the behavior of the internal <c>OutboxDbContextInterceptor</c> (auto-Outbox creation and OutboxId
///     back-fill) via its observable effects on the database. The interceptor is wired up by <c>UseOutbox()</c>.
/// </summary>
public class OutboxDbContextInterceptorTests
{
    private static OutboxTestDbContext CreateContext(string databaseName)
    {
        var builder = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseInMemoryDatabase(databaseName);
        builder.UseOutbox();
        return new OutboxTestDbContext(builder.Options);
    }

    [Fact]
    public async Task Interceptor_Creates_Single_Outbox_Per_SaveChanges_Batch()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(databaseName);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Act - add several messages within a single SaveChanges call
        for (var i = 0; i < 3; i++)
            context.AddOutboxMessage(new TestOrder { OrderNumber = $"ORD-{i}", Amount = i });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - exactly one Outbox created and all messages share its Id
        var outboxes = await context.Set<Outbox>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(outboxes);

        var messages = await context.Set<OutboxMessage>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, messages.Count);
        Assert.All(messages, m => Assert.Equal(outboxes[0].Id, m.OutboxId));
        Assert.All(messages, m => Assert.NotEqual(Guid.Empty, m.OutboxId));
    }

    [Fact]
    public async Task Interceptor_Creates_Distinct_Outbox_Per_SaveChanges_Call()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(databaseName);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Act - two separate SaveChanges calls should each produce their own outbox
        context.AddOutboxMessage(new TestOrder { OrderNumber = "ORD-A", Amount = 1 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.AddOutboxMessage(new TestOrder { OrderNumber = "ORD-B", Amount = 2 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - two outboxes, each with a single distinct message
        var outboxes = await context.Set<Outbox>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, outboxes.Count);

        var messages = await context.Set<OutboxMessage>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, messages.Count);

        var distinctOutboxIds = messages.Select(m => m.OutboxId).Distinct().ToList();
        Assert.Equal(2, distinctOutboxIds.Count);
    }

    [Fact]
    public async Task Interceptor_Reuses_Existing_Added_Outbox()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(databaseName);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Act - manually add an Outbox in the same change set, then add messages.
        // The interceptor must reuse the already-Added Outbox instead of creating a new one.
        var explicitOutbox = new Outbox { Id = Guid.CreateVersion7(), AddedAt = DateTimeOffset.UtcNow };
        context.Set<Outbox>().Add(explicitOutbox);

        context.AddOutboxMessage(new TestOrder { OrderNumber = "ORD-X", Amount = 1 });
        context.AddOutboxMessage(new TestOrder { OrderNumber = "ORD-Y", Amount = 2 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - only the explicit outbox exists and messages are linked to it
        var outboxes = await context.Set<Outbox>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(outboxes);
        Assert.Equal(explicitOutbox.Id, outboxes[0].Id);

        var messages = await context.Set<OutboxMessage>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, messages.Count);
        Assert.All(messages, m => Assert.Equal(explicitOutbox.Id, m.OutboxId));
    }

    [Fact]
    public async Task Interceptor_Does_Not_Create_Outbox_When_No_Messages_Added()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(databaseName);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Act - save an unrelated entity with no outbox messages
        context.Orders.Add(new TestOrder { OrderNumber = "ORD-NONE", Amount = 5 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - no outbox is spuriously created
        var outboxes = await context.Set<Outbox>().CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, outboxes);
    }
}