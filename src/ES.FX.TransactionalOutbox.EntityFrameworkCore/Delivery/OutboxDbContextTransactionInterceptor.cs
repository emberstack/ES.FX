using System.Collections.Concurrent;
using System.Data.Common;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;

/// <summary>
///     Interceptor used to signal the delivery process of the <see cref="Outbox" /> when <see cref="OutboxMessage" />
///     entities are added within a transaction
///     This interceptor is used to signal the delivery process when the transaction is committed
/// </summary>
internal class OutboxDbContextTransactionInterceptor : DbTransactionInterceptor
{
    private static readonly ConcurrentDictionary<Type, List<WeakReference>> DbContextDictionary = new();

    public override InterceptionResult TransactionCommitting(DbTransaction transaction, TransactionEventData eventData,
        InterceptionResult result)
    {
        OnOutboxSaving(eventData.Context);
        return base.TransactionCommitting(transaction, eventData, result);
    }

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = new())
    {
        OnOutboxSaving(eventData.Context);
        return await base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken)
            .ConfigureAwait(false);
    }

    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        OnOutboxSaving(eventData.Context);
        base.TransactionCommitted(transaction, eventData);
    }


    public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = new())
    {
        OnOutboxSaved(eventData.Context);
        await base.TransactionCommittedAsync(transaction, eventData, cancellationToken).ConfigureAwait(false);
    }


    public static void OnOutboxSaved(DbContext? context)
    {
        if (context is null) return;
        var type = context.GetType();
        DbContextDictionary.AddOrUpdate(context.GetType(),
            _ => [],
            (_, bag) =>
            {
                try
                {
                    var matches = bag.RemoveAll(s => context.Equals(s.Target));
                    if (matches > 0) OutboxDeliverySignal.GetChannel(type).Writer.TryWrite(type);
                    bag.RemoveAll(s => s is { IsAlive: true });
                    return bag;
                }
                catch
                {
                    // Ignored. This needs to complete successfully
                }

                return [];
            });
    }

    private static void OnOutboxSaving(DbContext? context)
    {
        if (context == null) return;
        if (!context.ChangeTracker.Entries<OutboxMessage>().Any()) return;

        DbContextDictionary.AddOrUpdate(context.GetType(),
            _ => [new WeakReference(context)],
            (_, bag) =>
            {
                try
                {
                    bag.RemoveAll(s => !s.IsAlive);
                    bag.Add(new WeakReference(context));
                    return bag;
                }
                catch
                {
                    // Ignored. This needs to complete successfully
                }

                return [];
            });
    }
}