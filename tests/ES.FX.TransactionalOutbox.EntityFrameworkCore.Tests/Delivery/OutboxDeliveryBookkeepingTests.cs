using System.Collections.Concurrent;
using ES.FX.TransactionalOutbox.Delivery;
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
///     Behavioral coverage for the per-message delivery bookkeeping stamped by
///     <see cref="OutboxDeliveryService{TDbContext}" /> before each hand-off:
///     <c>DeliveryAttempts++</c>, <c>DeliveryFirstAttemptedAt ??= now</c> and <c>DeliveryLastAttemptedAt = now</c>
///     (source lines 221-223), and the copy of those values into the <see cref="OutboxMessageContext" />
///     handed to the handler/fault handler (lines 238-242).
///     <para>
///         These assert the real observable values (the counter the handler sees on each retry, and the
///         values persisted on the <see cref="OutboxMessage" /> row between attempts) so that dropping the
///         increment, freezing/zeroing the counter, or skipping the timestamp stamping causes a failure.
///     </para>
///     Uses the EF Core InMemory provider for deterministic, container-free execution.
/// </summary>
public class OutboxDeliveryBookkeepingTests(ITestOutputHelper output)
{
    private static IHost BuildHost(
        string databaseName,
        IOutboxMessageHandler messageHandler,
        ITestOutputHelper output,
        IOutboxMessageFaultHandler? faultHandler = null,
        TimeSpan? pollingInterval = null)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
                logging.AddProvider(new XUnitLoggerProvider(output));
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
                    options.BatchSize = 10;
                    options.PollingInterval = pollingInterval ?? TimeSpan.FromMilliseconds(50);
                    options.DeliveryTimeout = TimeSpan.FromSeconds(5);
                });
            })
            .Build();
    }

    private static async Task SeedSingleMessageAsync(IHost host, string orderNumber)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        context.AddOutboxMessage(new TestOrder { OrderNumber = orderNumber, Amount = 1m });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string because)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(25);
        }

        if (!condition())
            throw new TimeoutException($"Condition was not met within {timeout}: {because}");
    }

    /// <summary>
    ///     A single message that fails twice then succeeds. The handler records the <see cref="OutboxMessageContext" />
    ///     it receives on every attempt. Asserts that:
    ///     the attempt counter the handler observes is exactly 1, 2, 3 across the three deliveries (kills
    ///     dropping <c>DeliveryAttempts++</c>, zeroing, or hardcoding a constant into the context);
    ///     <c>DeliveryFirstAttemptedAt</c> is stamped once and stays identical across retries (kills skipping the
    ///     <c>??=</c> stamp, which would otherwise fall back to a fresh <c>now</c> on each attempt);
    ///     <c>DeliveryLastAttemptedAt</c> is always populated and never precedes the first attempt (kills skipping
    ///     the <c>DeliveryLastAttemptedAt = now</c> stamp, which would surface null).
    /// </summary>
    [Fact]
    public async Task Delivery_Bookkeeping_Is_Observed_By_Handler_Across_Retries()
    {
        var databaseName = Guid.NewGuid().ToString();
        var handler = new ContextCapturingHandler(3);
        var faultHandler = new RedeliverFaultHandler(TimeSpan.FromMilliseconds(20));

        using var host = BuildHost(databaseName, handler, output, faultHandler);
        await SeedSingleMessageAsync(host, "RETRY-ME");

        await host.StartAsync(TestContext.Current.CancellationToken);

        // Wait for all three delivery attempts (2 faults + 1 success) to be observed.
        await WaitUntilAsync(() => handler.Contexts.Count >= 3, TimeSpan.FromSeconds(15),
            "handler should observe 3 delivery attempts");

        await host.StopAsync(TestContext.Current.CancellationToken);

        var contexts = handler.Contexts.ToList();
        Assert.True(contexts.Count >= 3, $"Expected at least 3 attempts, got {contexts.Count}");

        // The attempt counter must be pre-incremented per attempt: 1, then 2, then 3.
        Assert.Equal(1, contexts[0].DeliveryAttempts);
        Assert.Equal(2, contexts[1].DeliveryAttempts);
        Assert.Equal(3, contexts[2].DeliveryAttempts);

        // First-attempt timestamp is stamped once and carried unchanged through every retry.
        var firstStamp = contexts[0].DeliveryFirstAttemptedAt;
        Assert.NotEqual(default, firstStamp);
        Assert.All(contexts.Take(3), c => Assert.Equal(firstStamp, c.DeliveryFirstAttemptedAt));

        // Last-attempt timestamp is always populated and never earlier than the first attempt.
        Assert.All(contexts.Take(3), c =>
        {
            Assert.NotNull(c.DeliveryLastAttemptedAt);
            Assert.True(c.DeliveryLastAttemptedAt >= firstStamp,
                "DeliveryLastAttemptedAt must be at or after DeliveryFirstAttemptedAt");
        });

        // The last-attempt timestamp advances (each retry restamps it to a later 'now'), proving it is not
        // frozen to the first-attempt value.
        Assert.True(contexts[2].DeliveryLastAttemptedAt > contexts[0].DeliveryLastAttemptedAt,
            "DeliveryLastAttemptedAt should advance across retries");
    }

    /// <summary>
    ///     Inspects the persisted <see cref="OutboxMessage" /> row after exactly one faulted attempt (the fault
    ///     handler redelivers with a long delay so the row survives and is not reprocessed during the window).
    ///     Asserts the service persisted the bookkeeping onto the row: <c>DeliveryAttempts == 1</c>, both
    ///     timestamps set and equal on the first attempt. This kills mutations that drop the persisted
    ///     <c>DeliveryAttempts++</c> or the <c>DeliveryFirstAttemptedAt</c>/<c>DeliveryLastAttemptedAt</c> stamping.
    /// </summary>
    [Fact]
    public async Task Delivery_Bookkeeping_Is_Persisted_On_Message_After_First_Attempt()
    {
        var databaseName = Guid.NewGuid().ToString();
        var handler = new ContextCapturingHandler(int.MaxValue); // always fault
        // Long redeliver delay so the outbox stays delayed and the row is not attempted again in the window.
        var faultHandler = new RedeliverFaultHandler(TimeSpan.FromMinutes(10));

        using var host = BuildHost(databaseName, handler, output, faultHandler);
        await SeedSingleMessageAsync(host, "PERSIST-ME");

        var before = DateTimeOffset.UtcNow;
        await host.StartAsync(TestContext.Current.CancellationToken);

        // Wait for exactly one faulted attempt to be handled and committed.
        await WaitUntilAsync(() => faultHandler.FaultCount >= 1, TimeSpan.FromSeconds(15),
            "the message should fault once");
        // Give the transaction time to commit the stamped row.
        await Task.Delay(200, TestContext.Current.CancellationToken);

        await host.StopAsync(TestContext.Current.CancellationToken);
        var after = DateTimeOffset.UtcNow;

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
        var message = await context.Set<OutboxMessage>()
            .SingleAsync(TestContext.Current.CancellationToken);

        // Exactly one recorded attempt persisted on the row.
        Assert.Equal(1, message.DeliveryAttempts);

        // Both timestamps stamped within the run window; last is at or after first on a single attempt.
        Assert.NotNull(message.DeliveryFirstAttemptedAt);
        Assert.NotNull(message.DeliveryLastAttemptedAt);
        Assert.True(message.DeliveryLastAttemptedAt >= message.DeliveryFirstAttemptedAt);
        Assert.InRange(message.DeliveryFirstAttemptedAt!.Value, before, after);
        Assert.InRange(message.DeliveryLastAttemptedAt!.Value, before, after);
    }

    private sealed class ContextCapturingHandler(int failUntilAttempt) : IOutboxMessageHandler
    {
        private readonly ConcurrentQueue<OutboxMessageContext> _contexts = new();
        private int _attemptCount;

        public IReadOnlyList<OutboxMessageContext> Contexts => _contexts.ToList();

        public ValueTask HandleAsync(OutboxMessageContext context, CancellationToken cancellationToken = default)
        {
            _contexts.Enqueue(context);
            var attempt = Interlocked.Increment(ref _attemptCount);
            if (attempt < failUntilAttempt)
                throw new InvalidOperationException($"Simulated failure on attempt {attempt}");
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);
    }

    private sealed class RedeliverFaultHandler(TimeSpan delay) : IOutboxMessageFaultHandler
    {
        private int _faultCount;
        public int FaultCount => _faultCount;

        public ValueTask<DeliveryFaultResult> HandleAsync(OutboxMessageFaultContext context,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _faultCount);
            return ValueTask.FromResult(DeliveryFaultResult.Redeliver(delay));
        }
    }

    private sealed class XUnitLoggerProvider(ITestOutputHelper output) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new XUnitLogger(output, categoryName);

        public void Dispose()
        {
        }

        private sealed class XUnitLogger(ITestOutputHelper output, string categoryName) : ILogger
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