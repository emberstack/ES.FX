using ES.FX.TransactionalOutbox.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.MySql.Tests;

/// <summary>
///     Deterministic (no-container) tests for the entity-not-found guard in
///     <see cref="MySqlOutboxProvider{TDbContext}.GetNextExclusiveOutboxWithoutDelay" />.
///     The guard runs against the EF model before any SQL is executed, so the EF Core InMemory
///     provider is sufficient to exercise it.
/// </summary>
public class MySqlOutboxProviderGuardTests
{
    private static NoOutboxDbContext CreateContextWithoutOutbox() =>
        new(new DbContextOptionsBuilder<NoOutboxDbContext>()
            .UseInMemoryDatabase($"no-outbox-{Guid.NewGuid():N}")
            .Options);

    [Fact]
    public async Task GetNextExclusiveOutboxWithoutDelay_Throws_When_Outbox_Entity_Missing()
    {
        // Arrange
        await using var context = CreateContextWithoutOutbox();
        var provider = new MySqlOutboxProvider<NoOutboxDbContext>();

        // Act / Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetNextExclusiveOutboxWithoutDelay(context, TestContext.Current.CancellationToken));

        // The guard message must mention both the missing entity type and the offending context type.
        Assert.Contains(typeof(Outbox).ToString(), exception.Message);
        Assert.Contains(nameof(NoOutboxDbContext), exception.Message);
    }

    [Fact]
    public async Task GetNextExclusiveOutboxWithoutDelay_Throws_Before_Touching_Database()
    {
        // Arrange: the InMemory database is never created; if the guard did not short-circuit,
        // FromSqlRaw would fail with a different (provider) exception instead of the guard message.
        await using var context = CreateContextWithoutOutbox();
        var provider = new MySqlOutboxProvider<NoOutboxDbContext>();

        // Act / Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetNextExclusiveOutboxWithoutDelay(context, TestContext.Current.CancellationToken));

        Assert.StartsWith($"Entity type {typeof(Outbox)} not found", exception.Message);
    }

    /// <summary>
    ///     A context that intentionally does NOT call <c>AddOutbox()</c>, so the <see cref="Outbox" />
    ///     entity type is absent from the model.
    /// </summary>
    private sealed class NoOutboxDbContext(DbContextOptions<NoOutboxDbContext> options) : DbContext(options);
}