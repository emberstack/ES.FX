using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.MySql;

/// <summary>
///     MySQL implementation of <see cref="IOutboxProvider{TDbContext}" /> that retrieves the next exclusive (locked)
///     <see cref="Outbox" /> without a delivery delay.
/// </summary>
/// <remarks>
///     This implementation uses MySQL's row-level locking with FOR UPDATE SKIP LOCKED to ensure exclusive access
///     to the outbox without blocking concurrent transactions. Requires MySQL 8.0+ or MariaDB 10.6+.
/// </remarks>
[PublicAPI]
public class MySqlOutboxProvider<TDbContext> : IOutboxProvider<TDbContext>
    where TDbContext : DbContext
{
    /// <inheritdoc />
    public async Task<Outbox?> GetNextExclusiveOutboxWithoutDelay(TDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(Outbox)) ??
                         throw new InvalidOperationException(
                             $"Entity type {typeof(Outbox)} not found in {dbContext.GetType().Name}");

        var schema = entityType.GetSchema();
        var tableName = !string.IsNullOrEmpty(schema)
            ? $"`{schema}`.`{entityType.GetTableName()}`"
            : $"`{entityType.GetTableName()}`";

        var storeObject = StoreObjectIdentifier.Table(entityType.GetTableName()!, schema);
        var lockColumn = entityType.FindProperty(nameof(Outbox.Lock))!.GetColumnName(storeObject);
        var deliveryDelayedUntilColumn =
            entityType.FindProperty(nameof(Outbox.DeliveryDelayedUntil))!.GetColumnName(storeObject);
        var addedAtColumn = entityType.FindProperty(nameof(Outbox.AddedAt))!.GetColumnName(storeObject);

        // MySQL syntax for row-level locking with SKIP LOCKED to avoid blocking
        var query =
            $"SELECT * FROM {tableName} " +
            $"WHERE `{lockColumn}` IS NULL AND (`{deliveryDelayedUntilColumn}` IS NULL OR `{deliveryDelayedUntilColumn}` < {{0}}) " +
            $"ORDER BY `{addedAtColumn}` " +
            "LIMIT 1 " +
            "FOR UPDATE SKIP LOCKED";

        return await dbContext.Set<Outbox>()
            .FromSqlRaw(query, DateTimeOffset.UtcNow)
            .AsTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}