using System.Diagnostics;
using ES.FX.TransactionalOutbox.Delivery;
using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Internals;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore;

[PublicAPI]
public static class DbContextExtensions
{
    /// <summary>
    ///     Adds a message to the outbox. Use the <see cref="OutboxMessageDeliveryOptions" /> to configure the delivery
    ///     options.
    /// </summary>
    /// <typeparam name="TMessage"> <see cref="TMessage" /> type of message payload</typeparam>
    /// <param name="dbContext"> <see cref="DbContext" /> instance to add the message to</param>
    /// <param name="message">The message payload</param>
    /// <param name="deliveryOptions">The options used to configure the message delivery</param>
    public static void AddOutboxMessage<TMessage>(this DbContext dbContext, TMessage message,
        OutboxMessageDeliveryOptions? deliveryOptions = null)
        where TMessage : class
    {
        deliveryOptions ??= new OutboxMessageDeliveryOptions();

        var outboxDbContextOptions = dbContext.GetService<OutboxDbContextOptions>();

        outboxDbContextOptions.Serializer.Serialize(
            message,
            typeof(TMessage),
            new Dictionary<string, string>(),
            out var payloadType,
            out var serializedPayload,
            out var serializedHeaders);

        dbContext.Set<OutboxMessage>().Add(new OutboxMessage
        {
            AddedAt = DateTimeOffset.UtcNow,
            Headers = serializedHeaders,
            Payload = serializedPayload,
            PayloadType = payloadType,
            ActivityId = Activity.Current?.Id,
            DeliveryAttempts = 0,
            DeliveryFirstAttemptedAt = null,
            DeliveryLastAttemptedAt = null,
            DeliveryNotBefore = deliveryOptions.NotBefore,
            DeliveryNotAfter = deliveryOptions.NotAfter
        });
    }
}