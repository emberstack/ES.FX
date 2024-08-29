using System.Collections.Concurrent;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;

/// <summary>
///     Interceptor used to handle the saving of <see cref="Outbox" /> and <see cref="OutboxMessage" /> entities and
///     signalling the delivery process
/// </summary>
internal class OutboxDbContextSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly ConcurrentDictionary<Type, List<WeakReference>> DbContextDictionary = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        OnOutboxSaving(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        OnOutboxSaving(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        OnOutboxSaved(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        OnOutboxSaved(eventData.Context);
        return result;
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
                              Id = Guid.NewGuid()
                          });


        // Find all OutboxMessage entities that are being added
        addedMessages = context.ChangeTracker.Entries<OutboxMessage>()
            .Where(e => e.State == EntityState.Added && e.Entity.OutboxId == Guid.Empty)
            .ToList();

        foreach (var message in addedMessages) message.Entity.OutboxId = outboxEntry.Entity.Id;


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