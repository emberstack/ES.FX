using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

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
    /// <inheritdoc />
    public async Task<Outbox?> GetNextExclusiveOutboxWithoutDelay(TDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(Outbox)) ??
                         throw new InvalidOperationException(
                             $"Entity type {typeof(Outbox)} not found in {dbContext.GetType().Name}");

        var tableName = GetTableName(entityType);
        var storeObject = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());
        var lockColumn = GetColumnName(entityType, storeObject, nameof(Outbox.Lock));
        var deliveryDelayedUntilColumn = GetColumnName(entityType, storeObject, nameof(Outbox.DeliveryDelayedUntil));
        var addedAtColumn = GetColumnName(entityType, storeObject, nameof(Outbox.AddedAt));

        // PostgreSQL uses FOR UPDATE SKIP LOCKED to get exclusive access without blocking
        // Double quotes are used for identifiers in PostgreSQL
        var query =
            $"SELECT * FROM {tableName} " +
            $"WHERE \"{lockColumn}\" IS NULL AND (\"{deliveryDelayedUntilColumn}\" IS NULL OR \"{deliveryDelayedUntilColumn}\" < {{0}}) " +
            $"ORDER BY \"{addedAtColumn}\" " +
            "LIMIT 1 " +
            "FOR UPDATE SKIP LOCKED";

        return await dbContext.Set<Outbox>()
            .FromSqlRaw(query, DateTimeOffset.UtcNow)
            .AsTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static string GetTableName(IEntityType entityType)
    {
        var schema = entityType.GetSchema();
        var tableName = entityType.GetTableName();

        return !string.IsNullOrEmpty(schema)
            ? $"\"{schema}\".\"{tableName}\""
            : $"\"{tableName}\"";
    }

    private static string GetColumnName(IEntityType entityType, in StoreObjectIdentifier storeObject,
        string propertyName) =>
        entityType.FindProperty(propertyName)!.GetColumnName(storeObject) ?? propertyName;
}