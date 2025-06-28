using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.InMemory;

/// <summary>
///     InMemory implementation of the outbox provider.
///     Note: The InMemory database provider does not support optimistic concurrency (RowVersion),
///     so this provider simply returns the next available outbox without any locking mechanism.
///     This is suitable for unit testing scenarios where concurrency is not a concern.
///     For integration tests that need to verify concurrent behavior, use SQLite or a real database.
/// </summary>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
[PublicAPI]
public class InMemoryOutboxProvider<TDbContext> : IOutboxProvider<TDbContext>
    where TDbContext : DbContext
{
    public async Task<Outbox?> GetNextExclusiveOutboxWithoutDelay(TDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        // Simply return the next available outbox
        // The OutboxDeliveryService will set the Lock and rely on optimistic concurrency
        // However, InMemory provider doesn't support this, so concurrent access may occur in tests
        return await dbContext.Set<Outbox>()
            .Where(o => o.Lock == null &&
                        (o.DeliveryDelayedUntil == null || o.DeliveryDelayedUntil < DateTimeOffset.UtcNow))
            .OrderBy(o => o.AddedAt)
            .AsTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}