using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.PostgreSql;

/// <summary>
///     Provides PostgreSQL-specific implementation for outbox operations using FOR UPDATE SKIP LOCKED
///     for exclusive row locking without blocking concurrent transactions.
/// </summary>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
[PublicAPI]
public class PostgreSqlOutboxProvider<TDbContext> : IOutboxProvider<TDbContext>
    where TDbContext : DbContext
{
    public async Task<Outbox?> GetNextExclusiveOutboxWithoutDelay(TDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var tableName = GetTableName<Outbox>(dbContext);

        // PostgreSQL uses FOR UPDATE SKIP LOCKED to get exclusive access without blocking
        // Double quotes are used for identifiers in PostgreSQL
        var query =
            $"SELECT * FROM {tableName} " +
            $"WHERE \"{nameof(Outbox.Lock)}\" IS NULL AND (\"{nameof(Outbox.DeliveryDelayedUntil)}\" IS NULL OR \"{nameof(Outbox.DeliveryDelayedUntil)}\" < {{0}}) " +
            $"ORDER BY \"{nameof(Outbox.AddedAt)}\" " +
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
            ? $"\"{schema}\".\"{tableName}\""
            : $"\"{tableName}\"";
    }
}