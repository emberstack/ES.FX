using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.PostgreSql.Tests;

/// <summary>
///     Tests for the model guard branch of
///     <see cref="PostgreSqlOutboxProvider{TDbContext}.GetNextExclusiveOutboxWithoutDelay" />.
///     This branch executes before any database round-trip, so no container is required.
/// </summary>
public class PostgreSqlOutboxProviderModelGuardTests
{
    [Fact]
    public async Task GetNextExclusiveOutboxWithoutDelay_Throws_When_Outbox_Not_In_Model()
    {
        // Arrange - use a real Npgsql provider but with a context that never registers the Outbox entity.
        // No connection is opened because the guard fires before any SQL is executed.
        var options = new DbContextOptionsBuilder<NoOutboxDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;

        await using var context = new NoOutboxDbContext(options);
        var provider = new PostgreSqlOutboxProvider<NoOutboxDbContext>();

        // Act + Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetNextExclusiveOutboxWithoutDelay(context, TestContext.Current.CancellationToken));

        Assert.Contains(typeof(Outbox).ToString(), exception.Message);
        Assert.Contains(nameof(NoOutboxDbContext), exception.Message);
    }

    /// <summary>
    ///     A DbContext that maps a regular entity but deliberately does NOT call <c>AddOutbox()</c>,
    ///     so the <see cref="Outbox" /> entity type is not present in the model.
    /// </summary>
    private sealed class NoOutboxDbContext(DbContextOptions<NoOutboxDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestOrder>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
            });
            // Intentionally no modelBuilder.AddOutbox();
            base.OnModelCreating(modelBuilder);
        }
    }
}