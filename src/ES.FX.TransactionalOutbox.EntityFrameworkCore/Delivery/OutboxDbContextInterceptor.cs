using System.Collections.Concurrent;
using System.Data.Common;
using System.Runtime.CompilerServices;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;

/// <summary>
///     Interceptor used to handle the saving of <see cref="Outbox" /> and <see cref="OutboxMessage" /> entities and
///     signalling the delivery process
/// </summary>
internal class OutboxDbContextInterceptor : ISaveChangesInterceptor, IDbTransactionInterceptor
{
    private static readonly ConcurrentDictionary<Type, List<WeakReference?>> DbContextDictionary = new();

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        OnOutboxSaved(eventData.Context, true);
    }


    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = new())
    {
        OnOutboxSaved(eventData.Context, true);
        return Task.CompletedTask;
    }


    public InterceptionResult TransactionCommitting(DbTransaction transaction, TransactionEventData eventData,
        InterceptionResult result)
    {
        OnOutboxSaving(eventData.Context);
        return result;
    }

    public ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = new())
    {
        OnOutboxSaving(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        OnOutboxSaved(eventData.Context);
        return result;
    }

    public ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        OnOutboxSaved(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public InterceptionResult<int> SavingChanges(DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        OnOutboxSaving(eventData.Context);
        return result;
    }

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        OnOutboxSaving(eventData.Context);
        return ValueTask.FromResult(result);
    }


    public static void OnOutboxSaved(DbContext? context, bool transactionCommitted = false,
        [CallerMemberName] string? callerMemberName = null)
    {
        if (context is null) return;
        var type = context.GetType();

        // If the transaction is not committed and there is an active transaction, do not signal, wait for the transaction to commit
        if (!transactionCommitted && context.Database.CurrentTransaction is not null) return;

        DbContextDictionary.AddOrUpdate(context.GetType(),
            _ => [],
            (_, bag) =>
            {
                var matches = bag.RemoveAll(s => context.Equals(s?.Target));
                if (matches > 0)
                    OutboxDeliverySignal.GetChannel(type).Writer.TryWrite(callerMemberName ?? nameof(OnOutboxSaved));
                try
                {
                    bag.RemoveAll(s => !s?.IsAlive ?? true);
                }
                catch
                {
                    // Ignored. This is a best-effort cleanup
                }

                return bag;
            });
    }

    private static void OnOutboxSaving(DbContext? context)
    {
        if (context == null) return;

        var addedMessages = context.ChangeTracker.Entries<OutboxMessage>()
            .Where(e => e.State == EntityState.Added)
            .ToList();
        if (addedMessages.Count == 0) return;


        var outboxEntry = context.ChangeTracker
                              .Entries<Outbox>()
                              .FirstOrDefault(e => e.State == EntityState.Added) ??
                          context.Set<Outbox>().Add(new Outbox
                          {
                              AddedAt = DateTimeOffset.UtcNow,
                              Id = Guid.CreateVersion7()
                          });


        // Find all OutboxMessage entities that are being added
        addedMessages =
        [
            .. context.ChangeTracker.Entries<OutboxMessage>()
                .Where(e => e.State == EntityState.Added && e.Entity.OutboxId == Guid.Empty)
        ];

        foreach (var message in addedMessages) message.Entity.OutboxId = outboxEntry.Entity.Id;

        DbContextDictionary.AddOrUpdate(context.GetType(),
            _ => [new WeakReference(context)],
            (_, bag) =>
            {
                try
                {
                    bag.RemoveAll(s => !s?.IsAlive ?? true);
                }
                catch
                {
                    // Ignored. This is a best-effort cleanup
                }

                bag.Add(new WeakReference(context));
                return bag;
            });
    }
}