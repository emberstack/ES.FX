using System.Collections.Concurrent;
using ES.FX.TransactionalOutbox.Delivery;
using ES.FX.TransactionalOutbox.Delivery.Faults;
using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests;

/// <summary>
///     Base class for delivery tests that should be implemented by all providers
/// </summary>
public abstract class OutboxDeliveryTestsBase(ITestOutputHelper output)
{
    /// <summary>
    ///     Configure the DbContext for the specific provider
    /// </summary>
    protected abstract void ConfigureDbContext(DbContextOptionsBuilder optionsBuilder, string connectionString);

    /// <summary>
    ///     Configure the outbox delivery options for the specific provider
    /// </summary>
    protected abstract void ConfigureOutboxDelivery(OutboxDeliveryOptions<OutboxTestDbContext> options);

    /// <summary>
    ///     Get a unique connection string for each test to ensure isolation
    /// </summary>
    protected abstract Task<string> GetConnectionStringAsync();

    /// <summary>
    ///     Clean up after test (e.g., drop database)
    /// </summary>
    protected virtual Task CleanupAsync(string connectionString) => Task.CompletedTask;

    /// <summary>
    ///     Initialize the database schema. Override to use migrations instead of EnsureCreated.
    /// </summary>
    protected virtual async Task InitializeDatabaseAsync(OutboxTestDbContext context)
    {
        await context.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task Should_Deliver_Single_Message()
    {
        // Arrange
        var connectionString = await GetConnectionStringAsync();
        try
        {
            var testId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var messageHandler = new TestMessageHandler();

            using var host = await CreateHostAsync(connectionString, messageHandler);

            // Add a message before starting the host
            using (var scope = host.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
                await InitializeDatabaseAsync(context);

                context.AddOutboxMessage(new TestOrder { OrderNumber = $"TEST-{testId}-001", Amount = 100m });
                await context.SaveChangesAsync();
            }

            // Act - Start the host which starts the delivery service
            await host.StartAsync();

            // Wait for delivery
            await messageHandler.WaitForMessageCountAsync(1, TimeSpan.FromSeconds(10));

            // Assert
            Assert.Single(messageHandler.DeliveredMessages);
            var deliveredMessage = messageHandler.DeliveredMessages.First();
            Assert.Equal($"TEST-{testId}-001", deliveredMessage.OrderNumber);
            Assert.Equal(100m, deliveredMessage.Amount);

            // Give the service a moment to clean up the outbox after delivery
            await Task.Delay(100);

            // Verify the outbox is cleaned up
            using (var scope = host.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
                var remainingOutboxes = await context.Set<Outbox>().CountAsync();
                Assert.Equal(0, remainingOutboxes);
            }

            await host.StopAsync();
        }
        finally
        {
            await CleanupAsync(connectionString);
        }
    }

    [Fact]
    public async Task Should_Deliver_Multiple_Messages_In_Order()
    {
        // Arrange
        var connectionString = await GetConnectionStringAsync();
        try
        {
            var testId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var messageHandler = new TestMessageHandler();

            using var host = await CreateHostAsync(connectionString, messageHandler, batchSize: 2);

            // Add multiple messages
            using (var scope = host.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
                await InitializeDatabaseAsync(context);

                for (var i = 1; i <= 5; i++)
                {
                    context.AddOutboxMessage(new TestOrder
                        { OrderNumber = $"TEST-{testId}-{i:D3}", Amount = i * 100m });
                    await context.SaveChangesAsync();
                }
            }

            // Act
            await host.StartAsync();

            // Wait for all messages
            await messageHandler.WaitForMessageCountAsync(5, TimeSpan.FromSeconds(15));

            // Assert
            Assert.Equal(5, messageHandler.DeliveredMessages.Count);

            // Verify order
            var deliveredOrders = messageHandler.DeliveredMessages
                .Select(m => m.OrderNumber)
                .ToList();

            for (var i = 1; i <= 5; i++) Assert.Contains($"TEST-{testId}-{i:D3}", deliveredOrders);

            await host.StopAsync();
        }
        finally
        {
            await CleanupAsync(connectionString);
        }
    }

    [Fact]
    public virtual async Task Should_Handle_Concurrent_Delivery_Services()
    {
        // Arrange
        var connectionString = await GetConnectionStringAsync();
        try
        {
            var testId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var sharedMessageHandler = new TestMessageHandler();

            // Create multiple hosts sharing the same database
            var hosts = new List<IHost>();
            for (var i = 0; i < 3; i++)
            {
                var host = await CreateHostAsync(connectionString, sharedMessageHandler, batchSize: 1);
                hosts.Add(host);
            }

            // Add messages
            using (var scope = hosts[0].Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
                await InitializeDatabaseAsync(context);

                for (var i = 1; i <= 10; i++)
                {
                    context.AddOutboxMessage(new TestOrder
                        { OrderNumber = $"TEST-{testId}-{i:D3}", Amount = i * 100m });
                    await context.SaveChangesAsync();
                }
            }

            // Act - Start all hosts
            foreach (var host in hosts) await host.StartAsync();

            // Wait for all messages
            await sharedMessageHandler.WaitForMessageCountAsync(10, TimeSpan.FromSeconds(20));

            // Assert - Each message delivered exactly once
            Assert.Equal(10, sharedMessageHandler.DeliveredMessages.Count);

            var uniqueOrders = sharedMessageHandler.DeliveredMessages
                .Select(m => m.OrderNumber)
                .Distinct()
                .Count();
            Assert.Equal(10, uniqueOrders);

            // Stop all hosts
            foreach (var host in hosts)
            {
                await host.StopAsync();
                host.Dispose();
            }
        }
        finally
        {
            await CleanupAsync(connectionString);
        }
    }

    [Fact]
    public async Task Should_Respect_Scheduled_Delivery()
    {
        // Arrange
        var connectionString = await GetConnectionStringAsync();
        try
        {
            var testId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var messageHandler = new TestMessageHandler();

            using var host = await CreateHostAsync(connectionString, messageHandler,
                pollingInterval: TimeSpan.FromMilliseconds(100));

            // Add messages with different delivery times
            var now = DateTimeOffset.UtcNow;
            using (var scope = host.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
                await InitializeDatabaseAsync(context);

                // Immediate delivery
                context.AddOutboxMessage(new TestOrder { OrderNumber = $"TEST-{testId}-IMMEDIATE", Amount = 100m });
                await context.SaveChangesAsync();

                // Delayed delivery
                context.AddOutboxMessage(
                    new TestOrder { OrderNumber = $"TEST-{testId}-DELAYED", Amount = 200m },
                    new OutboxMessageDeliveryOptions { NotBefore = now.AddSeconds(2) });
                await context.SaveChangesAsync();
            }

            // Act
            await host.StartAsync();

            // Should only get immediate message first
            await messageHandler.WaitForMessageCountAsync(1, TimeSpan.FromSeconds(1));
            Assert.Single(messageHandler.DeliveredMessages);
            Assert.Equal($"TEST-{testId}-IMMEDIATE", messageHandler.DeliveredMessages.First().OrderNumber);

            // Wait for delayed message
            await messageHandler.WaitForMessageCountAsync(2, TimeSpan.FromSeconds(5));
            Assert.Equal(2, messageHandler.DeliveredMessages.Count);
            Assert.Contains(messageHandler.DeliveredMessages, m => m.OrderNumber == $"TEST-{testId}-DELAYED");

            await host.StopAsync();
        }
        finally
        {
            await CleanupAsync(connectionString);
        }
    }

    [Fact]
    public async Task Should_Handle_Faults_And_Retry()
    {
        // Arrange
        var connectionString = await GetConnectionStringAsync();
        try
        {
            var testId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var faultingHandler = new FaultingMessageHandler(3);
            var faultHandler = new TestFaultHandler();

            using var host = await CreateHostAsync(
                connectionString,
                faultingHandler,
                faultHandler,
                pollingInterval: TimeSpan.FromMilliseconds(100));

            // Add a message
            using (var scope = host.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
                await InitializeDatabaseAsync(context);

                context.AddOutboxMessage(new TestOrder { OrderNumber = $"TEST-{testId}-001", Amount = 100m });
                await context.SaveChangesAsync();
            }

            // Act
            await host.StartAsync();

            // Wait for successful delivery (after retries)
            await faultingHandler.WaitForSuccessfulDeliveryAsync(TimeSpan.FromSeconds(10));

            // Assert
            Assert.Equal(3, faultingHandler.AttemptCount);
            Assert.Equal(2, faultHandler.FaultCount); // Failed twice before succeeding
            Assert.Single(faultingHandler.DeliveredMessages);

            await host.StopAsync();
        }
        finally
        {
            await CleanupAsync(connectionString);
        }
    }

    private Task<IHost> CreateHostAsync(
        string connectionString,
        IOutboxMessageHandler messageHandler,
        IOutboxMessageFaultHandler? faultHandler = null,
        int batchSize = 10,
        TimeSpan? pollingInterval = null)
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
                logging.AddProvider(new XUnitLoggerProvider(output));
            })
            .ConfigureServices(services =>
            {
                services.AddDbContext<OutboxTestDbContext>(options =>
                {
                    ConfigureDbContext(options, connectionString);
                    options.UseOutbox();
                });


                services.AddKeyedSingleton<IOutboxMessageHandler>(typeof(OutboxTestDbContext), messageHandler);

                if (faultHandler is not null)
                    services.AddKeyedSingleton<IOutboxMessageFaultHandler>(typeof(OutboxTestDbContext), faultHandler);


                services.AddOutboxDeliveryService<OutboxTestDbContext>(options =>
                {
                    ConfigureOutboxDelivery(options);
                    options.BatchSize = batchSize;
                    options.PollingInterval = pollingInterval ?? TimeSpan.FromMilliseconds(500);
                    options.DeliveryTimeout = TimeSpan.FromSeconds(5);
                });
            })
            .Build();

        return Task.FromResult(host);
    }

    private class TestMessageHandler : IOutboxMessageHandler
    {
        private readonly ConcurrentBag<TestOrder> _deliveredMessages = new();
        private readonly SemaphoreSlim _semaphore = new(0);

        public IReadOnlyCollection<TestOrder> DeliveredMessages => _deliveredMessages.ToList();

        public ValueTask HandleAsync(OutboxMessageContext context, CancellationToken cancellationToken = default)
        {
            if (context.Message is TestOrder order)
            {
                _deliveredMessages.Add(order);
                _semaphore.Release();
            }

            return ValueTask.CompletedTask;
        }

        public async Task WaitForMessageCountAsync(int expectedCount, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow.Add(timeout);

            while (_deliveredMessages.Count < expectedCount && DateTime.UtcNow < deadline)
            {
                var remainingTime = deadline - DateTime.UtcNow;
                if (remainingTime <= TimeSpan.Zero)
                    break;

                await _semaphore.WaitAsync(Math.Min((int)remainingTime.TotalMilliseconds, 100));
            }

            if (_deliveredMessages.Count < expectedCount)
                throw new TimeoutException(
                    $"Expected {expectedCount} messages but only received {_deliveredMessages.Count} within {timeout}");
        }
    }

    private class FaultingMessageHandler(int failUntilAttempt) : IOutboxMessageHandler
    {
        private readonly ConcurrentBag<TestOrder> _deliveredMessages = new();
        private readonly SemaphoreSlim _successSemaphore = new(0);
        private int _attemptCount;

        public int AttemptCount => _attemptCount;
        public IReadOnlyCollection<TestOrder> DeliveredMessages => _deliveredMessages.ToList();

        public ValueTask HandleAsync(OutboxMessageContext context, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _attemptCount);

            if (_attemptCount < failUntilAttempt)
                throw new InvalidOperationException($"Simulated failure on attempt {_attemptCount}");

            if (context.Message is TestOrder order)
            {
                _deliveredMessages.Add(order);
                _successSemaphore.Release();
            }

            return ValueTask.CompletedTask;
        }

        public async Task WaitForSuccessfulDeliveryAsync(TimeSpan timeout)
        {
            if (!await _successSemaphore.WaitAsync(timeout))
                throw new TimeoutException($"Message was not successfully delivered within {timeout}");
        }
    }

    private class TestFaultHandler : IOutboxMessageFaultHandler
    {
        private int _faultCount;

        public int FaultCount => _faultCount;

        public ValueTask<DeliveryFaultResult> HandleAsync(OutboxMessageFaultContext context,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _faultCount);

            // Retry with exponential backoff
            var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, context.MessageContext.DeliveryAttempts));

            return ValueTask.FromResult(DeliveryFaultResult.Redeliver(delay));
        }
    }

    private class XUnitLoggerProvider(ITestOutputHelper output) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new XUnitLogger(output, categoryName);

        public void Dispose()
        {
        }

        private class XUnitLogger(ITestOutputHelper output, string categoryName) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                try
                {
                    output.WriteLine(
                        $"[{DateTime.UtcNow:HH:mm:ss.fff}] [{logLevel}] [{categoryName}] {formatter(state, exception)}");
                    if (exception != null) output.WriteLine(exception.ToString());
                }
                catch
                {
                    // Ignore exceptions from output
                }
            }

            private class NullScope : IDisposable
            {
                public static NullScope Instance { get; } = new();

                public void Dispose()
                {
                }
            }
        }
    }
}