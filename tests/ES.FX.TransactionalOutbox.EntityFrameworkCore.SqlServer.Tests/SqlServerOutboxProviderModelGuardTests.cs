using ES.FX.TransactionalOutbox.Entities;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.SqlServer.Tests;

/// <summary>
///     Fast, container-free unit tests for the model/mapping guard clauses in
///     <see cref="SqlServerOutboxProvider{TDbContext}" />. These assertions run entirely against the EF Core
///     model metadata — the guards throw before any SQL is executed, so no SQL Server connection is required.
/// </summary>
public class SqlServerOutboxProviderModelGuardTests
{
    private static DbContextOptions<TContext> SqlServerOptions<TContext>() where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>()
            // A well-formed but unreachable connection string. The guards under test run against
            // the model metadata and throw before any connection is opened.
            .UseSqlServer("Server=127.0.0.1;Database=NeverConnected;Connect Timeout=1;")
            .Options;

    [Fact]
    public async Task GetNextExclusiveOutboxWithoutDelay_When_Outbox_Not_In_Model_Throws()
    {
        // Arrange - a context that never maps the Outbox entity.
        await using var context = new NoOutboxContext(SqlServerOptions<NoOutboxContext>());
        var provider = new SqlServerOutboxProvider<NoOutboxContext>();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetNextExclusiveOutboxWithoutDelay(context, TestContext.Current.CancellationToken));

        // Assert - the "entity type not found" guard fired.
        Assert.Contains(typeof(Outbox).ToString(), exception.Message);
        Assert.Contains(nameof(NoOutboxContext), exception.Message);
    }

    [Fact]
    public async Task GetNextExclusiveOutboxWithoutDelay_When_Outbox_Not_Mapped_To_Table_Throws()
    {
        // Arrange - Outbox is in the model but mapped to a view (no table name).
        await using var context = new ViewOnlyOutboxContext(SqlServerOptions<ViewOnlyOutboxContext>());
        var provider = new SqlServerOutboxProvider<ViewOnlyOutboxContext>();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetNextExclusiveOutboxWithoutDelay(context, TestContext.Current.CancellationToken));

        // Assert - the "not mapped to a table" guard fired.
        Assert.Contains("not mapped to a table", exception.Message);
    }

    [Fact]
    public async Task GetNextExclusiveOutboxWithoutDelay_When_Required_Property_Not_Mapped_Throws()
    {
        // Arrange - Outbox mapped to a table, but the Lock property is ignored (absent from the model).
        await using var context = new MissingLockPropertyContext(SqlServerOptions<MissingLockPropertyContext>());
        var provider = new SqlServerOutboxProvider<MissingLockPropertyContext>();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetNextExclusiveOutboxWithoutDelay(context, TestContext.Current.CancellationToken));

        // Assert - the "property not found" guard fired for Outbox.Lock.
        Assert.Contains(nameof(Outbox.Lock), exception.Message);
        Assert.Contains("not found", exception.Message);
    }

    [PublicAPI]
    private sealed class NoOutboxContext(DbContextOptions<NoOutboxContext> options) : DbContext(options)
    {
        public DbSet<Placeholder> Placeholders { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Placeholder>();
    }

    [PublicAPI]
    private sealed class ViewOnlyOutboxContext(DbContextOptions<ViewOnlyOutboxContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            // Mapping to a view leaves the entity in the model but with a null table name.
            modelBuilder.Entity<Outbox>(entity => entity.ToView("OutboxView"));
    }

    [PublicAPI]
    private sealed class MissingLockPropertyContext(DbContextOptions<MissingLockPropertyContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<Outbox>(entity =>
            {
                entity.ToTable("__Outboxes");
                // Remove the Lock property from the model so the column-resolution guard trips.
                entity.Ignore(o => o.Lock);
            });
    }

    [PublicAPI]
    private sealed class Placeholder
    {
        public int Id { get; set; }
    }
}