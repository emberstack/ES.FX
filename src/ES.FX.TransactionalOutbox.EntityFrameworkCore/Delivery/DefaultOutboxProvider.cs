using ES.FX.ComponentModel.TransactionalOutbox;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;

public class DefaultOutboxProvider<TDbContext> : IOutboxProvider<TDbContext>
    where TDbContext : DbContext, IOutboxContext
{
    public Task<Outbox?> GetNextExclusiveOutboxWithoutDelay(TDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Set<Outbox>()
            .Where(o => o.Lock == null &&
                        (o.DeliveryDelayedUntil == null || o.DeliveryDelayedUntil < DateTimeOffset.UtcNow))
            .OrderBy(o => o.AddedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}