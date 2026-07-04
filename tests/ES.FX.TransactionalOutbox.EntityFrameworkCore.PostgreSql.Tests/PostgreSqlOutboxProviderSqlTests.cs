using ES.FX.TransactionalOutbox.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Testcontainers.PostgreSql;
using Xunit;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.PostgreSql.Tests;

/// <summary>
///     Confirms that <see cref="PostgreSqlOutboxProvider{TDbContext}" /> emits valid, correctly-quoted raw SQL
///     for schema-qualified tables, remapped columns, and honors FOR UPDATE SKIP LOCKED semantics against a
///     real PostgreSQL engine. These paths cannot be exercised without a live database because the private
///     GetTableName / GetColumnName helpers are only reachable through executed SQL: a wrong schema quote or a
///     wrong column name would surface as a SQL error rather than a wrong result.
/// </summary>
public class PostgreSqlOutboxProviderSqlTests : IAsyncLifetime
{
    private string _connectionString = null!;
    private PostgreSqlContainer _container = null!;

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await _container.StartAsync(TestContext.Current.CancellationToken);
        _connectionString = _container.GetConnectionString();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    private static Outbox NewOutbox(DateTimeOffset addedAt, Guid? @lock = null, DateTimeOffset? delayedUntil = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            AddedAt = addedAt,
            Lock = @lock,
            DeliveryDelayedUntil = delayedUntil
        };

    [Fact]
    public async Task GetNextExclusiveOutbox_Works_For_Schema_Qualified_Table()
    {
        var options = new DbContextOptionsBuilder<CustomSchemaDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        await using var context = new CustomSchemaDbContext(options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var expected = NewOutbox(DateTimeOffset.UtcNow.AddMinutes(-1));
        context.Set<Outbox>().Add(expected);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var provider = new PostgreSqlOutboxProvider<CustomSchemaDbContext>();

        // If GetTableName failed to emit "schema"."table" with correct quoting the raw SQL would throw
        // (relation does not exist) instead of returning the row.
        var result =
            await provider.GetNextExclusiveOutboxWithoutDelay(context, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(expected.Id, result.Id);
    }

    [Fact]
    public async Task GetNextExclusiveOutbox_Works_For_Remapped_Columns()
    {
        var options = new DbContextOptionsBuilder<CustomColumnsDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        await using var context = new CustomColumnsDbContext(options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Unlocked, undelayed row that should be selected.
        var ready = NewOutbox(DateTimeOffset.UtcNow.AddMinutes(-5));
        // Locked row that must be ignored (Lock IS NULL filter on the remapped column).
        var locked = NewOutbox(DateTimeOffset.UtcNow.AddMinutes(-10), Guid.NewGuid());
        // Delayed row that must be ignored (DeliveryDelayedUntil filter on the remapped column).
        var delayed = NewOutbox(DateTimeOffset.UtcNow.AddMinutes(-10),
            delayedUntil: DateTimeOffset.UtcNow.AddHours(1));

        context.Set<Outbox>().AddRange(ready, locked, delayed);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var provider = new PostgreSqlOutboxProvider<CustomColumnsDbContext>();

        // If any of the remapped column names were resolved incorrectly the SQL would throw (column does
        // not exist). A correct mapping also proves the WHERE/ORDER BY reference the right columns: only the
        // ready row satisfies "lock IS NULL AND (delayed IS NULL OR delayed < now)".
        var result =
            await provider.GetNextExclusiveOutboxWithoutDelay(context, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(ready.Id, result.Id);
    }

    private DbContextOptions<SkipLockDbContext> SkipLockOptions() =>
        new DbContextOptionsBuilder<SkipLockDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

    [Fact]
    public async Task GetNextExclusiveOutbox_SkipLocked_Yields_Distinct_Rows_To_Concurrent_Transactions()
    {
        // Arrange: two ready rows.
        await using (var seed = new SkipLockDbContext(SkipLockOptions()))
        {
            await seed.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            seed.Set<Outbox>().Add(NewOutbox(DateTimeOffset.UtcNow.AddMinutes(-2)));
            seed.Set<Outbox>().Add(NewOutbox(DateTimeOffset.UtcNow.AddMinutes(-1)));
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var provider = new PostgreSqlOutboxProvider<SkipLockDbContext>();

        // Two separate contexts / connections, each holding an open transaction so the row lock survives.
        await using var ctxA = new SkipLockDbContext(SkipLockOptions());
        await using var ctxB = new SkipLockDbContext(SkipLockOptions());

        await using var txA = await ctxA.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        // First consumer takes and holds a row.
        var rowA = await provider.GetNextExclusiveOutboxWithoutDelay(ctxA, TestContext.Current.CancellationToken);

        await using var txB = await ctxB.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        // Second consumer must SKIP the locked row and grab the other one (not block, not return the same row).
        var rowB = await provider.GetNextExclusiveOutboxWithoutDelay(ctxB, TestContext.Current.CancellationToken);

        Assert.NotNull(rowA);
        Assert.NotNull(rowB);
        Assert.NotEqual(rowA.Id, rowB.Id);

        await txA.RollbackAsync(TestContext.Current.CancellationToken);
        await txB.RollbackAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetNextExclusiveOutbox_Returns_Rows_In_AddedAt_Ascending_FIFO_Order()
    {
        // Arrange: seed five ready rows whose AddedAt order is deliberately unrelated to insertion order
        // and to Id order. Only "ORDER BY AddedAt" (FIFO) yields the sequence asserted below; removing the
        // ORDER BY, reversing it (DESC), or ordering by Id would produce a different sequence.
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-30);

        // (insertionIndex -> AddedAt offset in minutes). Insertion order is 0..4; AddedAt order is shuffled.
        // Expected FIFO (oldest AddedAt first): +1, +2, +3, +4, +5 minutes past baseTime.
        var seedByAddedAt = new[]
        {
            (added: baseTime.AddMinutes(3), label: 3),
            (added: baseTime.AddMinutes(1), label: 1),
            (added: baseTime.AddMinutes(5), label: 5),
            (added: baseTime.AddMinutes(2), label: 2),
            (added: baseTime.AddMinutes(4), label: 4)
        };

        var idByLabel = new Dictionary<int, Guid>();

        await using (var seed = new SkipLockDbContext(SkipLockOptions()))
        {
            await seed.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            foreach (var (added, label) in seedByAddedAt)
            {
                var row = NewOutbox(added);
                idByLabel[label] = row.Id;
                seed.Set<Outbox>().Add(row);
                // Save one at a time so insertion timestamps/heap order differ from AddedAt order.
                await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        var provider = new PostgreSqlOutboxProvider<SkipLockDbContext>();

        // Drain using one transaction per taken row: PostgreSQL SKIP LOCKED only skips rows locked by OTHER
        // transactions (a transaction can re-lock its own rows), so each consumer needs its own transaction.
        // Each consumer takes exactly one row and holds the lock, forcing the next consumer to the next
        // candidate in the provider's ordering.
        var contexts = new List<SkipLockDbContext>();
        var transactions = new List<IDbContextTransaction>();
        var returnedIds = new List<Guid>();
        try
        {
            for (var i = 0; i < seedByAddedAt.Length; i++)
            {
                var ctx = new SkipLockDbContext(SkipLockOptions());
                contexts.Add(ctx);
                var tx = await ctx.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
                transactions.Add(tx);

                var row = await provider.GetNextExclusiveOutboxWithoutDelay(ctx,
                    TestContext.Current.CancellationToken);
                Assert.NotNull(row);
                returnedIds.Add(row.Id);
            }
        }
        finally
        {
            foreach (var tx in transactions)
            {
                await tx.RollbackAsync(TestContext.Current.CancellationToken);
                await tx.DisposeAsync();
            }

            foreach (var ctx in contexts) await ctx.DisposeAsync();
        }

        // Assert: the drained sequence equals AddedAt-ascending (FIFO) order, by Id.
        var expectedFifoIds = new[]
        {
            idByLabel[1], idByLabel[2], idByLabel[3], idByLabel[4], idByLabel[5]
        };

        Assert.Equal(expectedFifoIds, returnedIds);
    }

    [Fact]
    public async Task GetNextExclusiveOutbox_Returns_Oldest_Ready_Row_First()
    {
        // Arrange: three ready rows with distinct AddedAt values inserted newest-first, so neither
        // insertion order nor a DESC ordering would surface the oldest row first.
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-30);
        var newest = NewOutbox(baseTime.AddMinutes(10));
        var middle = NewOutbox(baseTime.AddMinutes(5));
        var oldest = NewOutbox(baseTime.AddMinutes(1));

        await using (var seed = new SkipLockDbContext(SkipLockOptions()))
        {
            await seed.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            seed.Set<Outbox>().Add(newest);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
            seed.Set<Outbox>().Add(middle);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
            seed.Set<Outbox>().Add(oldest);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var provider = new PostgreSqlOutboxProvider<SkipLockDbContext>();

        await using var ctx = new SkipLockDbContext(SkipLockOptions());
        var row = await provider.GetNextExclusiveOutboxWithoutDelay(ctx, TestContext.Current.CancellationToken);

        Assert.NotNull(row);
        // FIFO: the row with the smallest AddedAt must be returned, not the newest or an arbitrary heap row.
        Assert.Equal(oldest.Id, row.Id);
    }

    [Fact]
    public async Task GetNextExclusiveOutbox_SkipLocked_Returns_Null_When_Only_Row_Is_Locked()
    {
        // Arrange: exactly one ready row.
        await using (var seed = new SkipLockDbContext(SkipLockOptions()))
        {
            await seed.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            seed.Set<Outbox>().Add(NewOutbox(DateTimeOffset.UtcNow.AddMinutes(-2)));
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var provider = new PostgreSqlOutboxProvider<SkipLockDbContext>();

        await using var ctxA = new SkipLockDbContext(SkipLockOptions());
        await using var ctxB = new SkipLockDbContext(SkipLockOptions());

        await using var txA = await ctxA.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var rowA = await provider.GetNextExclusiveOutboxWithoutDelay(ctxA, TestContext.Current.CancellationToken);
        Assert.NotNull(rowA);

        await using var txB = await ctxB.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        // The only row is locked by txA; SKIP LOCKED means B gets nothing instead of blocking or double-taking.
        var rowB = await provider.GetNextExclusiveOutboxWithoutDelay(ctxB, TestContext.Current.CancellationToken);
        Assert.Null(rowB);

        await txA.RollbackAsync(TestContext.Current.CancellationToken);
        await txB.RollbackAsync(TestContext.Current.CancellationToken);
    }

    // ---- Gap: schema-qualified branch of GetTableName ----

    private sealed class CustomSchemaDbContext(DbContextOptions<CustomSchemaDbContext> options) : DbContext(options)
    {
        public const string Schema = "outbox_schema";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Outbox>(entity =>
            {
                entity.ToTable("__Outboxes", Schema);
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RowVersion).IsRowVersion();
            });
            base.OnModelCreating(modelBuilder);
        }
    }

    // ---- Gap: custom column mapping in GetColumnName ----

    private sealed class CustomColumnsDbContext(DbContextOptions<CustomColumnsDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Outbox>(entity =>
            {
                entity.ToTable("__Outboxes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.AddedAt).HasColumnName("added_at_custom");
                entity.Property(e => e.Lock).HasColumnName("lock_custom");
                entity.Property(e => e.DeliveryDelayedUntil).HasColumnName("delayed_until_custom");
                entity.Property(e => e.RowVersion).IsRowVersion();
            });
            base.OnModelCreating(modelBuilder);
        }
    }

    // ---- Gap: FOR UPDATE SKIP LOCKED concurrency ----

    private sealed class SkipLockDbContext(DbContextOptions<SkipLockDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Outbox>(entity =>
            {
                entity.ToTable("__Outboxes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RowVersion).IsRowVersion();
            });
            base.OnModelCreating(modelBuilder);
        }
    }
}