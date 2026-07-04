using System.Collections.Concurrent;
using System.Reflection;
using ES.FX.TransactionalOutbox.Delivery;
using ES.FX.TransactionalOutbox.Delivery.Actions;
using ES.FX.TransactionalOutbox.Delivery.Faults;
using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Delivery;

/// <summary>
///     Drives specific branches of <see cref="OutboxDeliveryService{TDbContext}" /> that the shared base suite does not
///     cover: message expiry, the Discard fault action, the unsupported-action guard, disabled service, the
///     handler-not-ready gate, and batch-overflow outbox retention. Uses the EF Core InMemory provider so the tests are
///     deterministic and fast (no container required).
/// </summary>
public class OutboxDeliveryServiceBranchTests(ITestOutputHelper output)
{
    private static IHost BuildHost(
        string databaseName,
        IOutboxMessageHandler messageHandler,
        ITestOutputHelper output,
        IOutboxMessageFaultHandler? faultHandler = null,
        int batchSize = 10,
        bool deliveryServiceEnabled = true,
        TimeSpan? pollingInterval = null)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
                logging.AddProvider(new TestOutputLoggerProvider(output));
            })
            .ConfigureServices(services =>
            {
                services.AddDbContext<OutboxTestDbContext>(options =>
                {
                    options.UseInMemoryDatabase(databaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                    options.UseOutbox();
                });

                services.AddKeyedSingleton<IOutboxMessageHandler>(typeof(OutboxTestDbContext), messageHandler);

                if (faultHandler is not null)
                    services.AddKeyedSingleton<IOutboxMessageFaultHandler>(typeof(OutboxTestDbContext), faultHandler);

                services.AddOutboxDeliveryService<OutboxTestDbContext>(options =>
                {
                    options.BatchSize = batchSize;
                    options.PollingInterval = pollingInterval ?? TimeSpan.FromMilliseconds(100);
                    options.DeliveryTimeout = TimeSpan.FromSeconds(5);
                    options.DeliveryServiceEnabled = deliveryServiceEnabled;
                });
            })
            .Build();
    }

    private static async Task SeedDatabaseAsync(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<int> CountOutboxesAsync(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
        return await context.Set<Outbox>().CountAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<int> CountMessagesAsync(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
        return await context.Set<OutboxMessage>().CountAsync(TestContext.Current.CancellationToken);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return;
            await Task.Delay(50);
        }

        if (!await condition())
            throw new TimeoutException($"Condition was not met within {timeout}");
    }

    [Fact]
    public async Task Expired_Message_Is_Dropped_Without_Being_Delivered()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var handler = new RecordingMessageHandler();
        using var host = BuildHost(databaseName, handler, output);
        await SeedDatabaseAsync(host);

        // Add an already-expired message (NotAfter in the past) plus a valid one.
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
            context.AddOutboxMessage(new TestOrder { OrderNumber = "EXPIRED", Amount = 1m },
                new OutboxMessageDeliveryOptions { NotAfter = DateTimeOffset.UtcNow.AddMinutes(-5) });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            context.AddOutboxMessage(new TestOrder { OrderNumber = "VALID", Amount = 2m });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        await host.StartAsync(TestContext.Current.CancellationToken);

        // The valid message should be delivered; the expired one dropped. Wait for both outboxes to clear.
        await WaitUntilAsync(async () => await CountOutboxesAsync(host) == 0, TimeSpan.FromSeconds(10));

        // Assert - only the valid message reached the handler; expired never delivered.
        Assert.DoesNotContain(handler.Delivered, o => o.OrderNumber == "EXPIRED");
        Assert.Contains(handler.Delivered, o => o.OrderNumber == "VALID");

        // Both messages are removed from the store (valid delivered, expired discarded).
        Assert.Equal(0, await CountMessagesAsync(host));

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Fault_Discard_Removes_Message_And_Proceeds()
    {
        // Arrange - handler always throws; fault handler asks to Discard.
        var databaseName = Guid.NewGuid().ToString();
        var handler = new AlwaysThrowingMessageHandler();
        var faultHandler = new DiscardingFaultHandler();
        using var host = BuildHost(databaseName, handler, output, faultHandler);
        await SeedDatabaseAsync(host);

        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
            context.AddOutboxMessage(new TestOrder { OrderNumber = "DISCARD-ME", Amount = 1m });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        await host.StartAsync(TestContext.Current.CancellationToken);

        // The message must be discarded (removed) and the outbox drained.
        await WaitUntilAsync(async () => await CountOutboxesAsync(host) == 0, TimeSpan.FromSeconds(10));

        // Assert
        Assert.True(faultHandler.FaultCount >= 1);
        Assert.Equal(0, await CountMessagesAsync(host));

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeliveryServiceDisabled_Delivers_Nothing()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var handler = new RecordingMessageHandler();
        using var host = BuildHost(databaseName, handler, output, deliveryServiceEnabled: false,
            pollingInterval: TimeSpan.FromMilliseconds(50));
        await SeedDatabaseAsync(host);

        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
            context.AddOutboxMessage(new TestOrder { OrderNumber = "SHOULD-NOT-DELIVER", Amount = 1m });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act - run for a while; the disabled service must only poll/sleep.
        await host.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);

        // Assert - nothing delivered and the message remains in the outbox.
        Assert.Empty(handler.Delivered);
        Assert.Equal(1, await CountMessagesAsync(host));
        Assert.Equal(1, await CountOutboxesAsync(host));

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handler_Not_Ready_Skips_Delivery_Then_Delivers_When_Ready()
    {
        // Arrange - handler reports not-ready initially, then flips to ready.
        var databaseName = Guid.NewGuid().ToString();
        var handler = new GatedMessageHandler();
        using var host = BuildHost(databaseName, handler, output,
            pollingInterval: TimeSpan.FromMilliseconds(50));
        await SeedDatabaseAsync(host);

        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
            context.AddOutboxMessage(new TestOrder { OrderNumber = "GATED", Amount = 1m });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        await host.StartAsync(TestContext.Current.CancellationToken);

        // While not ready, nothing should be delivered.
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Empty(handler.Delivered);
        Assert.True(handler.IsReadyCallCount >= 1, "IsReadyAsync should have been polled while gated");

        // Flip to ready; delivery must now happen.
        handler.SetReady();
        await WaitUntilAsync(async () => await CountOutboxesAsync(host) == 0, TimeSpan.FromSeconds(10));

        // Assert
        Assert.Contains(handler.Delivered, o => o.OrderNumber == "GATED");

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Batch_Overflow_Retains_Outbox_For_Next_Pass_And_Eventually_Drains()
    {
        // Arrange - a single outbox with more messages than the batch size.
        // The full-batch branch keeps the outbox (outboxHasRemainingMessages=true) so it is not removed
        // until all messages are drained across multiple passes.
        var databaseName = Guid.NewGuid().ToString();
        var handler = new RecordingMessageHandler();
        using var host = BuildHost(databaseName, handler, output, batchSize: 2,
            pollingInterval: TimeSpan.FromMilliseconds(50));
        await SeedDatabaseAsync(host);

        // All 5 messages land in a single outbox (single SaveChanges batch).
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
            for (var i = 1; i <= 5; i++)
                context.AddOutboxMessage(new TestOrder { OrderNumber = $"MSG-{i:D2}", Amount = i });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Sanity: exactly one outbox holds all five messages.
        Assert.Equal(1, await CountOutboxesAsync(host));
        Assert.Equal(5, await CountMessagesAsync(host));

        // Act
        await host.StartAsync(TestContext.Current.CancellationToken);

        // Eventually every message is delivered and the outbox removed.
        await WaitUntilAsync(async () => await CountOutboxesAsync(host) == 0, TimeSpan.FromSeconds(15));

        // Assert - all five delivered exactly once, in a batch size that forced multiple passes.
        Assert.Equal(5, handler.Delivered.Count);
        Assert.Equal(5, handler.Delivered.Select(o => o.OrderNumber).Distinct().Count());
        Assert.Equal(0, await CountMessagesAsync(host));

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Fault_Unknown_Action_Retains_Message_And_Does_Not_Deliver()
    {
        // Arrange - the fault handler returns a DeliveryFaultResult carrying an action that is neither
        // Redeliver nor Discard. The service must hit the NotSupportedException guard, which aborts the
        // batch (no commit), so the message is neither delivered nor removed and keeps being retried.
        var databaseName = Guid.NewGuid().ToString();
        var handler = new AlwaysThrowingMessageHandler();
        var faultHandler = new UnknownActionFaultHandler();
        using var host = BuildHost(databaseName, handler, output, faultHandler,
            pollingInterval: TimeSpan.FromMilliseconds(50));
        await SeedDatabaseAsync(host);

        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
            context.AddOutboxMessage(new TestOrder { OrderNumber = "UNKNOWN-ACTION", Amount = 1m });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act - let the service attempt (and fail) to process the message repeatedly.
        await host.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(async () => await Task.FromResult(faultHandler.FaultCount >= 1),
            TimeSpan.FromSeconds(10));
        await Task.Delay(300, TestContext.Current.CancellationToken);

        // Assert - the fault handler was invoked, but the unsupported action means the message was never
        // removed (batch aborted without commit) and never handed off as delivered.
        Assert.True(faultHandler.FaultCount >= 1);
        Assert.Equal(1, await CountMessagesAsync(host));
        Assert.Equal(1, await CountOutboxesAsync(host));

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private sealed class RecordingMessageHandler : IOutboxMessageHandler
    {
        private readonly ConcurrentBag<TestOrder> _delivered = new();
        public IReadOnlyCollection<TestOrder> Delivered => _delivered.ToList();

        public ValueTask HandleAsync(OutboxMessageContext context, CancellationToken cancellationToken = default)
        {
            if (context.Message is TestOrder order)
                _delivered.Add(order);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class AlwaysThrowingMessageHandler : IOutboxMessageHandler
    {
        public ValueTask HandleAsync(OutboxMessageContext context, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated permanent failure");
    }

    private sealed class GatedMessageHandler : IOutboxMessageHandler
    {
        private readonly ConcurrentBag<TestOrder> _delivered = new();
        private int _isReadyCallCount;
        private volatile bool _ready;

        public IReadOnlyCollection<TestOrder> Delivered => _delivered.ToList();
        public int IsReadyCallCount => _isReadyCallCount;

        public ValueTask HandleAsync(OutboxMessageContext context, CancellationToken cancellationToken = default)
        {
            if (context.Message is TestOrder order)
                _delivered.Add(order);
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _isReadyCallCount);
            return ValueTask.FromResult(_ready);
        }

        public void SetReady() => _ready = true;
    }

    private sealed class DiscardingFaultHandler : IOutboxMessageFaultHandler
    {
        private int _faultCount;
        public int FaultCount => _faultCount;

        public ValueTask<DeliveryFaultResult> HandleAsync(OutboxMessageFaultContext context,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _faultCount);
            return ValueTask.FromResult(DeliveryFaultResult.Discard());
        }
    }

    /// <summary>
    ///     An <see cref="IMessageAction" /> that is neither Redeliver nor Discard, used to exercise the
    ///     NotSupportedException guard in the delivery service.
    /// </summary>
    private sealed class UnknownMessageAction : IMessageAction
    {
    }

    private sealed class UnknownActionFaultHandler : IOutboxMessageFaultHandler
    {
        // DeliveryFaultResult only exposes Discard()/Redeliver() publicly. Build one carrying an unknown
        // action via its private constructor so we can drive the service's unsupported-action branch.
        private static readonly DeliveryFaultResult UnknownResult = CreateUnknownResult();

        private int _faultCount;
        public int FaultCount => _faultCount;

        public ValueTask<DeliveryFaultResult> HandleAsync(OutboxMessageFaultContext context,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _faultCount);
            return ValueTask.FromResult(UnknownResult);
        }

        private static DeliveryFaultResult CreateUnknownResult()
        {
            var ctor = typeof(DeliveryFaultResult).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                [typeof(IMessageAction)],
                null);
            Assert.NotNull(ctor);
            return (DeliveryFaultResult)ctor!.Invoke([new UnknownMessageAction()]);
        }
    }

    private sealed class TestOutputLoggerProvider(ITestOutputHelper output) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new TestOutputLogger(output, categoryName);

        public void Dispose()
        {
        }

        private sealed class TestOutputLogger(ITestOutputHelper output, string categoryName) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                try
                {
                    output.WriteLine($"[{logLevel}] [{categoryName}] {formatter(state, exception)}");
                    if (exception is not null) output.WriteLine(exception.ToString());
                }
                catch
                {
                    // Ignore output failures (e.g. test already completed)
                }
            }

            private sealed class NullScope : IDisposable
            {
                public static NullScope Instance { get; } = new();

                public void Dispose()
                {
                }
            }
        }
    }
}