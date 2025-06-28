using ES.FX.TransactionalOutbox.Delivery;
using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Extensions;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.InMemory.Tests;

public class SimpleFunctionalTests
{
    [Fact]
    public Task UseInMemoryOutboxProvider_Should_Configure_Provider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<OutboxTestDbContext>(options =>
        {
            options.UseInMemoryDatabase(Guid.NewGuid().ToString());
            options.UseOutbox();
        });

        services.AddOutboxDeliveryService<OutboxTestDbContext, TestMessageHandler>(options =>
        {
            options.UseInMemoryOutboxProvider();
        });

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var deliveryOptions =
            serviceProvider.GetRequiredService<IOptions<OutboxDeliveryOptions<OutboxTestDbContext>>>();

        // Assert
        Assert.NotNull(deliveryOptions.Value.OutboxProvider);
        Assert.IsType<InMemoryOutboxProvider<OutboxTestDbContext>>(deliveryOptions.Value.OutboxProvider);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task InMemoryProvider_Should_Get_Single_Outbox()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<OutboxTestDbContext>(options =>
        {
            options.UseInMemoryDatabase(Guid.NewGuid().ToString());
            options.UseOutbox();
        });

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
        await context.Database.EnsureCreatedAsync();

        // Add a single outbox
        var outbox = new Outbox
        {
            Id = Guid.CreateVersion7(),
            AddedAt = DateTimeOffset.UtcNow
        };
        context.Set<Outbox>().Add(outbox);
        await context.SaveChangesAsync();

        // Act
        var provider = new InMemoryOutboxProvider<OutboxTestDbContext>();
        var result = await provider.GetNextExclusiveOutboxWithoutDelay(context);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(outbox.Id, result.Id);
    }

    [Fact]
    public async Task InMemoryProvider_Does_Not_Support_Concurrent_Locking()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<OutboxTestDbContext>(options =>
        {
            options.UseInMemoryDatabase(databaseName);
            options.UseOutbox();
        }, ServiceLifetime.Transient);

        var serviceProvider = services.BuildServiceProvider();

        // Setup initial data - just one outbox
        using (var scope = serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
            await context.Database.EnsureCreatedAsync();

            var outbox = new Outbox
            {
                Id = Guid.CreateVersion7(),
                AddedAt = DateTimeOffset.UtcNow
            };
            context.Set<Outbox>().Add(outbox);
            await context.SaveChangesAsync();
        }

        // Act - Try to get the same outbox concurrently
        var provider = new InMemoryOutboxProvider<OutboxTestDbContext>();
        var tasks = new List<Task<Outbox?>>();

        for (var i = 0; i < 3; i++)
            tasks.Add(Task.Run(async () =>
            {
                using var taskScope = serviceProvider.CreateScope();
                var taskContext = taskScope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
                return await provider.GetNextExclusiveOutboxWithoutDelay(taskContext);
            }));

        var results = await Task.WhenAll(tasks);

        // Assert - InMemory doesn't support optimistic concurrency, so multiple tasks may get the same outbox
        // This is expected behavior for InMemory provider
        var nonNullResults = results.Where(r => r != null).ToList();
        Assert.True(nonNullResults.Count >= 1); // At least one should get the outbox

        // Note: In a real database with RowVersion support, only one would succeed
        // when the OutboxDeliveryService tries to set the Lock
    }

    [Fact]
    public async Task InMemoryProvider_Should_Skip_Locked_Outboxes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<OutboxTestDbContext>(options =>
        {
            options.UseInMemoryDatabase(Guid.NewGuid().ToString());
            options.UseOutbox();
        });

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
        await context.Database.EnsureCreatedAsync();

        // Add an outbox that is already locked
        var lockedOutbox = new Outbox
        {
            Id = Guid.CreateVersion7(),
            AddedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            Lock = Guid.CreateVersion7()
        };
        context.Set<Outbox>().Add(lockedOutbox);

        // Add an available outbox
        var availableOutbox = new Outbox
        {
            Id = Guid.CreateVersion7(),
            AddedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        context.Set<Outbox>().Add(availableOutbox);

        await context.SaveChangesAsync();

        // Act
        var provider = new InMemoryOutboxProvider<OutboxTestDbContext>();
        var result = await provider.GetNextExclusiveOutboxWithoutDelay(context);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(availableOutbox.Id, result.Id);
        Assert.Null(result.Lock);
    }

    private class TestMessageHandler : IOutboxMessageHandler
    {
        public readonly List<object> DeliveredMessages = new();

        public ValueTask HandleAsync(OutboxMessageContext context,
            CancellationToken cancellationToken = default)
        {
            DeliveredMessages.Add(context.Message);
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);
    }
}