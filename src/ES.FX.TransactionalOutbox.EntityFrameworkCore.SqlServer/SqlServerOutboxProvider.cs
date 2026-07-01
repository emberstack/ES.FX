using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.SqlServer;

/// <summary>
///     SQL Server optimized provider used to get the next exclusive (locked) <see cref="Outbox" /> which does not have a
///     delivery delay
/// </summary>
/// <typeparam name="TDbContext"></typeparam>
[PublicAPI]
public class SqlServerOutboxProvider<TDbContext> : IOutboxProvider<TDbContext>
    where TDbContext : DbContext
{
    /// <inheritdoc />
    public async Task<Outbox?> GetNextExclusiveOutboxWithoutDelay(TDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(Outbox)) ??
                         throw new InvalidOperationException(
                             $"Entity type {typeof(Outbox)} not found in {dbContext.GetType().Name}");

        var tableName = entityType.GetTableName() ??
                        throw new InvalidOperationException(
                            $"Entity type {entityType.DisplayName()} is not mapped to a table");
        var schema = entityType.GetSchema();
        var qualifiedTableName = string.IsNullOrWhiteSpace(schema)
            ? $"[{tableName}]"
            : $"[{schema}].[{tableName}]";

        var storeObject = StoreObjectIdentifier.Table(tableName, schema);
        var lockColumn = GetColumnName(entityType, storeObject, nameof(Outbox.Lock));
        var deliveryDelayedUntilColumn = GetColumnName(entityType, storeObject, nameof(Outbox.DeliveryDelayedUntil));
        var addedAtColumn = GetColumnName(entityType, storeObject, nameof(Outbox.AddedAt));

        //Get the next outbox message that is not delayed, ordered by the time it was added. Read past the locked ones.
        var query =
            $"SELECT TOP 1 * FROM {qualifiedTableName} WITH (UPDLOCK, ROWLOCK, READPAST) " +
            $"WHERE [{lockColumn}] IS NULL  AND ( [{deliveryDelayedUntilColumn}] IS NULL OR [{deliveryDelayedUntilColumn}] < {{0}} )" +
            $"ORDER BY [{addedAtColumn}]";

        var outbox = await dbContext.Set<Outbox>()
            .FromSqlRaw(query, DateTimeOffset.UtcNow)
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return outbox;
    }

    private static string GetColumnName(IEntityType entityType, in StoreObjectIdentifier storeObject,
        string propertyName)
    {
        var property = entityType.FindProperty(propertyName) ??
                       throw new InvalidOperationException(
                           $"Property {propertyName} not found on {entityType.DisplayName()}");

        return property.GetColumnName(storeObject) ??
               throw new InvalidOperationException(
                   $"Column for property {propertyName} not found on {entityType.DisplayName()}");
    }
}