using ES.FX.TransactionalOutbox.Delivery;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Delivery;

public class OutboxDeliveryTests(ITestOutputHelper output) : OutboxDeliveryTestsBase(output)
{
    protected override void ConfigureDbContext(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        optionsBuilder.UseInMemoryDatabase(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
    }

    protected override void ConfigureOutboxDelivery(OutboxDeliveryOptions<OutboxTestDbContext> options)
    {
    }

    protected override Task<string> GetConnectionStringAsync()
    {
        // Use a unique database name for each test to ensure isolation
        var databaseName = $"OutboxTest_{Guid.NewGuid():N}";
        return Task.FromResult(databaseName);
    }


    [Fact]
    public async Task InMemory_Provider_Should_Handle_Sequential_Processing()
    {
        // This test verifies that InMemory provider works correctly
        // when messages are processed sequentially (single service instance)

        // Arrange
        var connectionString = await GetConnectionStringAsync();
        var testId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var messageHandler = new TestMessageHandler();

        using var host = await CreateHostAsync(connectionString, messageHandler, 1);

        // Add multiple messages
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();

            for (var i = 1; i <= 3; i++)
            {
                context.AddOutboxMessage(new TestOrder { OrderNumber = $"TEST-{testId}-{i:D3}", Amount = i * 100m });
                await context.SaveChangesAsync();
            }
        }

        // Act
        await host.StartAsync();

        // Wait for all messages
        await messageHandler.WaitForMessageCountAsync(3, TimeSpan.FromSeconds(10));

        // Assert
        Assert.Equal(3, messageHandler.DeliveredMessages.Count);

        // Verify all messages were delivered
        var deliveredOrders = messageHandler.DeliveredMessages
            .Select(m => m.OrderNumber)
            .OrderBy(x => x)
            .ToList();

        Assert.Equal($"TEST-{testId}-001", deliveredOrders[0]);
        Assert.Equal($"TEST-{testId}-002", deliveredOrders[1]);
        Assert.Equal($"TEST-{testId}-003", deliveredOrders[2]);

        await host.StopAsync();
    }

    private static Task<IHost> CreateHostAsync(
        string connectionString,
        IOutboxMessageHandler messageHandler,
        int batchSize = 10)
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => { logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning); })
            .ConfigureServices(services =>
            {
                services.AddDbContext<OutboxTestDbContext>(options =>
                {
                    options.UseInMemoryDatabase(connectionString)
                        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                    options.UseOutbox();
                });

                // Register the handler as scoped since OutboxDeliveryService expects scoped handlers
                services.AddKeyedScoped<IOutboxMessageHandler>(typeof(OutboxTestDbContext), (_, _) => messageHandler);

                services.AddOutboxDeliveryService<OutboxTestDbContext>(options =>
                {
                    options.BatchSize = batchSize;
                    options.PollingInterval = TimeSpan.FromMilliseconds(100);
                    options.DeliveryTimeout = TimeSpan.FromSeconds(5);
                });
            })
            .Build();

        return Task.FromResult(host);
    }

    private class TestMessageHandler : IOutboxMessageHandler
    {
        private readonly List<TestOrder> _deliveredMessages = new();
        private readonly SemaphoreSlim _semaphore = new(0);

        public IReadOnlyCollection<TestOrder> DeliveredMessages
        {
            get
            {
                lock (_deliveredMessages)
                {
                    return _deliveredMessages.ToList();
                }
            }
        }

        public ValueTask HandleAsync(OutboxMessageContext context, CancellationToken cancellationToken = default)
        {
            if (context.Message is TestOrder order)
            {
                lock (_deliveredMessages)
                {
                    _deliveredMessages.Add(order);
                }

                _semaphore.Release();
            }

            return ValueTask.CompletedTask;
        }

        public async Task WaitForMessageCountAsync(int expectedCount, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow.Add(timeout);

            while (true)
            {
                lock (_deliveredMessages)
                {
                    if (_deliveredMessages.Count >= expectedCount)
                        return;
                }

                if (DateTime.UtcNow >= deadline)
                    break;

                var remainingTime = deadline - DateTime.UtcNow;
                if (remainingTime <= TimeSpan.Zero)
                    break;

                await _semaphore.WaitAsync(Math.Min((int)remainingTime.TotalMilliseconds, 100));
            }

            int actualCount;
            lock (_deliveredMessages)
            {
                actualCount = _deliveredMessages.Count;
            }

            if (actualCount < expectedCount)
                throw new TimeoutException(
                    $"Expected {expectedCount} messages but only received {actualCount} within {timeout}");
        }
    }
}