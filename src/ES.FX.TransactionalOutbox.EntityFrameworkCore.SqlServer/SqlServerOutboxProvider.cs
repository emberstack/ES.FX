using ES.FX.Messaging;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.SqlServer;

/// <summary>
///     SQL Server optimized provider used to get the next exclusive (locked) <see cref="Outbox" /> which does not have a
///     delivery delay
/// </summary>
/// <typeparam name="TDbContext"></typeparam>
public class SqlServerOutboxProvider<TDbContext> : IOutboxProvider<TDbContext>
    where TDbContext : DbContext, IMessageStore
{
    public async Task<Outbox?> GetNextExclusiveOutboxWithoutDelay(TDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var tableName = GetTableName<Outbox>(dbContext);

        //Get the next outbox message that is not delayed, ordered by the time it was added. Read past the locked ones.
        var query =
            $"SELECT TOP 1 * FROM {tableName} WITH (UPDLOCK, ROWLOCK, READPAST) " +
            $"WHERE {nameof(Outbox.Lock)} IS NULL  AND ( {nameof(Outbox.DeliveryDelayedUntil)} IS NULL OR {nameof(Outbox.DeliveryDelayedUntil)} < {{0}} )" +
            $"ORDER BY {nameof(Outbox.AddedAt)}";

        var outbox = await dbContext.Set<Outbox>()
            .FromSqlRaw(query, DateTimeOffset.UtcNow)
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return outbox;
    }

    private static string GetTableName<TEntity>(DbContext context) where TEntity : class
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity)) ??
                         throw new InvalidOperationException(
                             $"Entity type {typeof(TEntity)} not found in {context.GetType().Name}");

        var tableName = entityType.GetTableName();
        var schema = entityType.GetSchema();
        return string.IsNullOrWhiteSpace(schema) ? $"[{tableName}]" : $"[{schema}].[{tableName}]";
    }
}