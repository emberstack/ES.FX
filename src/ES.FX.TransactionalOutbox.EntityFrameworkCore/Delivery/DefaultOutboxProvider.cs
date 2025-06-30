using ES.FX.TransactionalOutbox.Entities;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;

/// <summary>
///     Default implementation of <see cref="IOutboxProvider{TDbContext}" /> that retrieves the next exclusive (locked)
///     <see cref="Outbox" /> without a delivery delay.
/// </summary>
[PublicAPI]
public class DefaultOutboxProvider<TDbContext> : IOutboxProvider<TDbContext>
    where TDbContext : DbContext
{
    public async Task<Outbox?> GetNextExclusiveOutboxWithoutDelay(TDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<Outbox>()
            .Where(o => o.Lock == null &&
                        (o.DeliveryDelayedUntil == null || o.DeliveryDelayedUntil < DateTimeOffset.UtcNow))
            .OrderBy(o => o.AddedAt)
            .AsTracking()
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}