using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.MySql;

/// <summary>
///     MySQL implementation of <see cref="IOutboxProvider{TDbContext}" /> that retrieves the next exclusive (locked)
///     <see cref="Outbox" /> without a delivery delay.
/// </summary>
/// <remarks>
///     This implementation uses MySQL's row-level locking with FOR UPDATE SKIP LOCKED to ensure exclusive access
///     to the outbox without blocking concurrent transactions.
/// </remarks>
[PublicAPI]
public class MySqlOutboxProvider<TDbContext> : IOutboxProvider<TDbContext>
    where TDbContext : DbContext
{
    public async Task<Outbox?> GetNextExclusiveOutboxWithoutDelay(TDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var tableName = GetTableName<Outbox>(dbContext);

        // MySQL syntax for row-level locking with SKIP LOCKED to avoid blocking
        var query =
            $"SELECT * FROM {tableName} " +
            $"WHERE `{nameof(Outbox.Lock)}` IS NULL AND (`{nameof(Outbox.DeliveryDelayedUntil)}` IS NULL OR `{nameof(Outbox.DeliveryDelayedUntil)}` < {{0}}) " +
            $"ORDER BY `{nameof(Outbox.AddedAt)}` " +
            "LIMIT 1 " +
            "FOR UPDATE SKIP LOCKED";

        return await dbContext.Set<Outbox>()
            .FromSqlRaw(query, DateTimeOffset.UtcNow)
            .AsTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static string GetTableName<TEntity>(TDbContext dbContext) where TEntity : class
    {
        var entityType = dbContext.Model.FindEntityType(typeof(TEntity));
        var schema = entityType?.GetSchema();
        var tableName = entityType?.GetTableName();

        return !string.IsNullOrEmpty(schema)
            ? $"`{schema}`.`{tableName}`"
            : $"`{tableName}`";
    }
}