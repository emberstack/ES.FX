using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MariaDb;
using Xunit;
using Xunit.Sdk;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.MySql.Tests;

/// <summary>
///     Real-service (MariaDB container) tests that pin the <c>FOR UPDATE SKIP LOCKED</c> semantics of
///     <see cref="MySqlOutboxProvider{TDbContext}.GetNextExclusiveOutboxWithoutDelay" /> - the non-blocking,
///     exclusive read that is the entire reason this provider exists over a plain locking read.
/// </summary>
/// <remarks>
///     These tests hold a <b>real database row lock</b> in one open transaction while a second, independent
///     transaction reads. With genuine <c>SKIP LOCKED</c> the second reader steps over the locked row
///     (grabbing a different eligible row, or returning null when none remain) <b>without blocking</b>. If the
///     provider degraded to plain <c>FOR UPDATE</c> (no <c>SKIP LOCKED</c>), the second reader would block on
///     the held lock; the bounded timeout on that read turns the block into a failed assertion instead of a
///     wrong-but-passing result. This is the specific mutation that survives every other test in the suite.
/// </remarks>
public class MySqlOutboxProviderSkipLockedTests : IAsyncLifetime
{
    /// <summary>
    ///     Upper bound for the "second reader" call. It is far above the sub-millisecond latency of a genuine
    ///     non-blocking SKIP LOCKED read, yet finite so a blocking plain <c>FOR UPDATE</c> surfaces as a
    ///     timeout (assertion failure) rather than an indefinite hang.
    /// </summary>
    private static readonly TimeSpan SecondReaderBudget = TimeSpan.FromSeconds(10);

    private string _connectionString = null!;
    private MariaDbContainer _mariaDbContainer = null!;

    public async ValueTask InitializeAsync()
    {
        _mariaDbContainer = new MariaDbBuilder("mariadb:latest").Build();
        await _mariaDbContainer.StartAsync(TestContext.Current.CancellationToken);
        _connectionString = _mariaDbContainer.GetConnectionString();
    }

    public async ValueTask DisposeAsync() => await _mariaDbContainer.DisposeAsync();

    private OutboxTestDbContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseMySql(_connectionString, ServerVersion.AutoDetect(_connectionString),
                o => o.MigrationsAssembly(typeof(MySqlOutboxProviderSkipLockedTests).Assembly.FullName));
        builder.UseOutbox();
        return new OutboxTestDbContext(builder.Options);
    }

    /// <summary>
    ///     Runs the provider read under a hard wall-clock deadline. A genuine SKIP LOCKED read completes well
    ///     within the budget even when another transaction holds a conflicting row lock; a plain blocking
    ///     <c>FOR UPDATE</c> would exceed it and fail the test.
    /// </summary>
    private static async Task<Outbox?> ReadWithinBudget(OutboxTestDbContext context)
    {
        using var cts = new CancellationTokenSource(SecondReaderBudget);
        var provider = new MySqlOutboxProvider<OutboxTestDbContext>();
        try
        {
            return await provider.GetNextExclusiveOutboxWithoutDelay(context, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new XunitException(
                $"Second reader did not complete within {SecondReaderBudget.TotalSeconds:0}s. " +
                "This indicates the read BLOCKED on a row lock held by another transaction, which is exactly " +
                "the behaviour of a plain 'FOR UPDATE' - the 'SKIP LOCKED' clause is missing or ineffective.");
        }
    }

    private async Task SeedReadyOutbox(string orderNumber, decimal amount)
    {
        await using var context = CreateContext();
        context.AddOutboxMessage(new TestOrder { OrderNumber = orderNumber, Amount = amount });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task EnsureCreatedAndCleared()
    {
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await context.Set<OutboxMessage>().ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        await context.Set<Outbox>().ExecuteDeleteAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SkipLocked_Second_Reader_Gets_The_Other_Row_Without_Blocking()
    {
        // Arrange: two ready (unlocked, undelayed) rows.
        await EnsureCreatedAndCleared();
        await SeedReadyOutbox("SKIP-A", 10m);
        // Nudge the second row later so ORDER BY AddedAt is well-defined and the two reads are deterministic.
        await Task.Delay(50, TestContext.Current.CancellationToken);
        await SeedReadyOutbox("SKIP-B", 20m);

        await using var ctxA = CreateContext();
        await using var ctxB = CreateContext();

        var provider = new MySqlOutboxProvider<OutboxTestDbContext>();

        // Reader A opens a transaction, takes the first eligible row, and HOLDS the lock (tx stays open).
        await using var txA = await ctxA.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var rowA = await provider.GetNextExclusiveOutboxWithoutDelay(ctxA, TestContext.Current.CancellationToken);
        Assert.NotNull(rowA);

        // Reader B, in its own transaction, must SKIP A's locked row and return the OTHER one immediately.
        await using var txB = await ctxB.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var rowB = await ReadWithinBudget(ctxB);

        // Observable, mutation-killing assertions:
        //  - B got a row at all (plain FOR UPDATE would have blocked -> ReadWithinBudget throws).
        //  - B got a DIFFERENT row than A (SKIP LOCKED stepped over the locked row rather than re-reading it).
        Assert.NotNull(rowB);
        Assert.NotEqual(rowA.Id, rowB.Id);

        await txB.RollbackAsync(TestContext.Current.CancellationToken);
        await txA.RollbackAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SkipLocked_Second_Reader_Gets_Null_When_Only_Row_Is_Locked()
    {
        // Arrange: exactly ONE ready row.
        await EnsureCreatedAndCleared();
        await SeedReadyOutbox("ONLY-ROW", 30m);

        await using var ctxA = CreateContext();
        await using var ctxB = CreateContext();

        var provider = new MySqlOutboxProvider<OutboxTestDbContext>();

        // Reader A locks the single eligible row and keeps its transaction open.
        await using var txA = await ctxA.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var rowA = await provider.GetNextExclusiveOutboxWithoutDelay(ctxA, TestContext.Current.CancellationToken);
        Assert.NotNull(rowA);

        // Reader B must return null RIGHT AWAY (skip the locked row) rather than block waiting for A's lock.
        await using var txB = await ctxB.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var rowB = await ReadWithinBudget(ctxB);

        // With plain FOR UPDATE, B would block until A commits/rolls back (caught as a timeout above). With
        // SKIP LOCKED, B observes no eligible unlocked row and returns null.
        Assert.Null(rowB);

        await txB.RollbackAsync(TestContext.Current.CancellationToken);
        await txA.RollbackAsync(TestContext.Current.CancellationToken);
    }
}