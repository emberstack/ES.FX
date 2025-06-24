using ES.FX.TransactionalOutbox.Entities;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;

/// <summary>
///     Provider used to get the next exclusive (locked) <see cref="Outbox" /> which does not have a delivery delay
/// </summary>
/// <typeparam name="TDbContext"></typeparam>
public interface IOutboxProvider<in TDbContext> where TDbContext : DbContext
{
    /// <summary>
    ///     Returns the next exclusive (locked) <see cref="Outbox" /> which does not have a delivery delay
    /// </summary>
    /// <param name="dbContext">The <see cref="TDbContext" /> from which to return the <see cref="Outbox" /> </param>
    /// <param name="cancellationToken">The <see cref="CancellationToken" /> for the operation</param>
    Task<Outbox?> GetNextExclusiveOutboxWithoutDelay(TDbContext dbContext,
        CancellationToken cancellationToken = default);
}