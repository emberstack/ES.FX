using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.SqlServer.Tests;

/// <summary>
///     Real-SQL-Server tests for the query behavior of
///     <see cref="SqlServerOutboxProvider{TDbContext}.GetNextExclusiveOutboxWithoutDelay" />: the WHERE filter
///     (no-eligible-row → null), the concurrent skip-locked (UPDLOCK/ROWLOCK/READPAST) semantics, and the
///     custom-schema table qualification branch. These require a real relational engine because the provider
///     issues raw SQL with SQL-Server-specific lock hints.
/// </summary>
public class SqlServerOutboxProviderQueryTests : IAsyncLifetime
{
    private const string Registry = "mcr.microsoft.com";
    private const string Image = "mssql/server";
    private const string Tag = "2025-latest";

    private string? _connectionString;
    private MsSqlContainer? _msSqlContainer;

    public async ValueTask InitializeAsync()
    {
        _msSqlContainer = new MsSqlBuilder($"{Registry}/{Image}:{Tag}")
            .WithImage("mcr.microsoft.com/mssql/server:2025-latest")
            .Build();

        await _msSqlContainer.StartAsync(TestContext.Current.CancellationToken);
        _connectionString = _msSqlContainer.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_msSqlContainer != null) await _msSqlContainer.DisposeAsync();
    }

    private OutboxTestDbContext CreateContext()
    {
        if (_connectionString == null)
            throw new InvalidOperationException("Container not initialized. Test setup failed.");

        var builder = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseSqlServer(_connectionString,
                o => o.MigrationsAssembly(typeof(SqlServerOutboxProviderQueryTests).Assembly.FullName));
        builder.UseOutbox();
        return new OutboxTestDbContext(builder.Options);
    }

    private static Outbox NewOutbox(DateTimeOffset addedAt, Guid? @lock = null, DateTimeOffset? delayedUntil = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            AddedAt = addedAt,
            Lock = @lock,
            DeliveryDelayedUntil = delayedUntil
        };

    [Fact]
    public async Task Returns_Null_When_Table_Empty()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var provider = new SqlServerOutboxProvider<OutboxTestDbContext>();

        // Act
        var result =
            await provider.GetNextExclusiveOutboxWithoutDelay(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Returns_Null_When_All_Rows_Locked_Or_Delayed()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var now = DateTimeOffset.UtcNow;

        // A locked row (Lock IS NOT NULL) - must be skipped by the WHERE clause.
        context.Set<Outbox>().Add(NewOutbox(now.AddMinutes(-5), Guid.NewGuid()));
        // A still-delayed row (DeliveryDelayedUntil in the future) - must be skipped.
        context.Set<Outbox>().Add(NewOutbox(now.AddMinutes(-4), delayedUntil: now.AddHours(1)));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var provider = new SqlServerOutboxProvider<OutboxTestDbContext>();

        // Act
        var result =
            await provider.GetNextExclusiveOutboxWithoutDelay(context, TestContext.Current.CancellationToken);

        // Assert - neither row is eligible, so SingleOrDefault yields null.
        Assert.Null(result);
    }

    [Fact]
    public async Task Returns_Oldest_Eligible_Row_And_Skips_Ineligible()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var now = DateTimeOffset.UtcNow;

        var locked = NewOutbox(now.AddMinutes(-10), Guid.NewGuid());
        var delayed = NewOutbox(now.AddMinutes(-9), delayedUntil: now.AddHours(1));
        // An expired-delay row IS eligible (DeliveryDelayedUntil < now).
        var eligibleOlder = NewOutbox(now.AddMinutes(-8), delayedUntil: now.AddMinutes(-1));
        var eligibleNewer = NewOutbox(now.AddMinutes(-2));

        context.Set<Outbox>().AddRange(locked, delayed, eligibleOlder, eligibleNewer);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var provider = new SqlServerOutboxProvider<OutboxTestDbContext>();

        // Act
        var result =
            await provider.GetNextExclusiveOutboxWithoutDelay(context, TestContext.Current.CancellationToken);

        // Assert - ordered by AddedAt, the oldest eligible row wins (expired-delay one).
        Assert.NotNull(result);
        Assert.Equal(eligibleOlder.Id, result!.Id);
    }

    [Fact]
    public async Task Concurrent_Callers_Skip_Row_Locked_By_Open_Transaction()
    {
        // Arrange - two eligible rows. One caller opens a transaction and grabs the oldest with
        // UPDLOCK/ROWLOCK; a second caller on a separate connection must READPAST it and get the other row.
        await using var seedContext = CreateContext();
        await seedContext.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var now = DateTimeOffset.UtcNow;
        var first = NewOutbox(now.AddMinutes(-10));
        var second = NewOutbox(now.AddMinutes(-5));
        seedContext.Set<Outbox>().AddRange(first, second);
        await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var provider = new SqlServerOutboxProvider<OutboxTestDbContext>();

        await using var contextA = CreateContext();
        await using var contextB = CreateContext();

        // Act
        await using var transactionA =
            await contextA.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Caller A locks the oldest eligible row and holds the lock inside the open transaction.
        var rowA = await provider.GetNextExclusiveOutboxWithoutDelay(contextA, TestContext.Current.CancellationToken);
        Assert.NotNull(rowA);
        Assert.Equal(first.Id, rowA!.Id);

        // Caller B (separate connection/transaction) must skip the locked row via READPAST.
        await using var transactionB =
            await contextB.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var rowB = await provider.GetNextExclusiveOutboxWithoutDelay(contextB, TestContext.Current.CancellationToken);

        // Assert - B did not block and did not receive the same row A holds.
        Assert.NotNull(rowB);
        Assert.NotEqual(rowA.Id, rowB!.Id);
        Assert.Equal(second.Id, rowB.Id);

        await transactionB.RollbackAsync(TestContext.Current.CancellationToken);
        await transactionA.RollbackAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Second_Caller_Gets_Null_When_Only_Row_Is_Locked()
    {
        // Arrange - a single eligible row. A holds it in an open transaction; B must READPAST and get null
        // (proving the lock hints skip rather than block, and that no double-delivery is possible).
        await using var seedContext = CreateContext();
        await seedContext.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var only = NewOutbox(DateTimeOffset.UtcNow.AddMinutes(-10));
        seedContext.Set<Outbox>().Add(only);
        await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var provider = new SqlServerOutboxProvider<OutboxTestDbContext>();

        await using var contextA = CreateContext();
        await using var contextB = CreateContext();

        // Act
        await using var transactionA =
            await contextA.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var rowA = await provider.GetNextExclusiveOutboxWithoutDelay(contextA, TestContext.Current.CancellationToken);
        Assert.NotNull(rowA);

        await using var transactionB =
            await contextB.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var rowB = await provider.GetNextExclusiveOutboxWithoutDelay(contextB, TestContext.Current.CancellationToken);

        // Assert - the only row is locked away from B.
        Assert.Null(rowB);

        await transactionB.RollbackAsync(TestContext.Current.CancellationToken);
        await transactionA.RollbackAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Resolves_Custom_Schema_Qualified_Table()
    {
        // Arrange - Outbox mapped to a non-default schema exercises the [schema].[table] branch and the
        // schema-qualified StoreObjectIdentifier column resolution.
        if (_connectionString == null)
            throw new InvalidOperationException("Container not initialized.");

        var builder = new DbContextOptionsBuilder<CustomSchemaOutboxContext>()
            .UseSqlServer(_connectionString);
        await using var context = new CustomSchemaOutboxContext(builder.Options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var eligible = NewOutbox(DateTimeOffset.UtcNow.AddMinutes(-10));
        context.Set<Outbox>().Add(eligible);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var provider = new SqlServerOutboxProvider<CustomSchemaOutboxContext>();

        // Act
        var result =
            await provider.GetNextExclusiveOutboxWithoutDelay(context, TestContext.Current.CancellationToken);

        // Assert - the schema-qualified query found and returned the row.
        Assert.NotNull(result);
        Assert.Equal(eligible.Id, result!.Id);
    }

    /// <summary>
    ///     A context that maps <see cref="Outbox" /> to a custom (non-dbo) schema so the provider's
    ///     <c>[schema].[table]</c> qualification path is exercised end-to-end.
    /// </summary>
    [PublicAPI]
    private sealed class CustomSchemaOutboxContext(DbContextOptions<CustomSchemaOutboxContext> options)
        : DbContext(options)
    {
        public const string SchemaName = "outboxtest";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(SchemaName);
            modelBuilder.Entity<Outbox>(entity =>
            {
                entity.ToTable("__Outboxes", SchemaName);
                entity.HasKey(o => o.Id);
                entity.Property(o => o.RowVersion).IsRowVersion();
            });
            base.OnModelCreating(modelBuilder);
        }
    }
}