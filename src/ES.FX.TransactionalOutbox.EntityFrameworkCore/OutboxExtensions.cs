using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Messages;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore;

[PublicAPI]
public static class OutboxExtensions
{
    /// <summary>
    ///     Adds the required Outbox entities to the model builder
    /// </summary>
    /// <param name="modelBuilder">The <see cref="ModelBuilder" /> to configure </param>
    public static void AddOutboxEntities(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Outbox>().ToTable("__Outboxes");
        modelBuilder.ApplyConfiguration(new Outbox.EntityTypeConfiguration());

        modelBuilder.Entity<OutboxMessage>().ToTable("__OutboxMessages");
        modelBuilder.ApplyConfiguration(new OutboxMessage.EntityTypeConfiguration());

        modelBuilder.Entity<OutboxMessageFault>().ToTable("__OutboxMessageFaults");
        modelBuilder.ApplyConfiguration(new OutboxMessageFault.EntityTypeConfiguration());
    }

    /// <summary>
    ///     Adds the Outbox behavior to the <see cref="DbContextOptionsBuilder" />. This enables signaling for delivery and
    ///     grouping of messages in outboxes.
    /// </summary>
    /// <param name="optionsBuilder"></param>
    public static void AddOutboxBehavior(this DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(
            new OutboxDbContextSaveChangesInterceptor(),
            new OutboxDbContextTransactionInterceptor());
    }


    /// <summary>
    ///     Adds a message to the outbox. Use the <see cref="OutboxMessageOptions" /> to configure the delivery options.
    /// </summary>
    /// <typeparam name="TOutboxDbContext">The <see cref="TOutboxDbContext" /> used to add the message</typeparam>
    /// <typeparam name="TMessage"> <see cref="TMessage" /> type of message payload</typeparam>
    /// <param name="dbContext"> <see cref="TOutboxDbContext" /> instance to add the message to</param>
    /// <param name="message">The message payload</param>
    /// <param name="deliveryOptions">The options used to configure the message delivery</param>
    public static void AddOutboxMessage<TOutboxDbContext, TMessage>(this TOutboxDbContext dbContext, TMessage message,
        OutboxMessageOptions? deliveryOptions = default)
        where TOutboxDbContext : DbContext, IOutboxDbContext
        where TMessage : class
    {
        deliveryOptions ??= new OutboxMessageOptions();
        var headers = new Dictionary<string, string?>
        {
            { OutboxMessageHeaders.Diagnostics.ActivityId, Activity.Current?.Id },
            { OutboxMessageHeaders.Message.PayloadOriginalType, typeof(TMessage).AssemblyQualifiedName }
        };

        var typeAttribute =
            message.GetType().GetCustomAttribute(typeof(OutboxMessageTypeAttribute)) as OutboxMessageTypeAttribute;
        var payloadType = typeAttribute?.MessageType ?? message.GetType().AssemblyQualifiedName!;


        dbContext.Set<OutboxMessage>().Add(new OutboxMessage
        {
            AddedAt = DateTimeOffset.UtcNow,
            PayloadType = payloadType,
            Payload = JsonSerializer.Serialize(message),
            DeliveryAttempts = 0,
            DeliveryFirstAttemptedAt = null,
            DeliveryLastAttemptError = null,
            DeliveryLastAttemptedAt = null,
            DeliveryNotBefore = deliveryOptions.NotBefore,
            DeliveryNotAfter = deliveryOptions.NotAfter,
            Headers = JsonSerializer.Serialize(headers),
            DeliveryMaxAttempts = deliveryOptions.MaxAttempts,
            DeliveryAttemptDelay = deliveryOptions.DelayBetweenAttempts,
            DeliveryAttemptDelayIsExponential = deliveryOptions.DelayBetweenAttemptsIsExponential
        });
    }


    /// <summary>
    ///     Registers a message type as a known type. This is used to determine the <see cref="Type" /> of the payload.
    /// </summary>
    public static void AddOutboxMessageType(this IServiceCollection serviceCollection, Type messageType)
    {
        if (!messageType.IsClass || messageType.IsAbstract)
            throw new ArgumentException("Message type must be a non-abstract class", nameof(messageType));
        serviceCollection.AddSingleton(typeof(IOutboxMessageType),
            typeof(OutboxMessageType<>).MakeGenericType(messageType));
    }

    /// <summary>
    ///     Registers a message type as a known type. This is used to determine the <see cref="Type" /> of the payload.
    /// </summary>
    public static void AddOutboxMessageType<TMessageType>(this IServiceCollection serviceCollection)
        where TMessageType : class =>
        serviceCollection.AddOutboxMessageType(typeof(TMessageType));


    /// <summary>
    ///     Registers known message types. This is used to determine the <see cref="Type" /> of the payload.
    /// </summary>
    public static void AddOutboxMessageTypes(this IServiceCollection serviceCollection, params Type[] types)
    {
        foreach (var type in types) serviceCollection.AddOutboxMessageType(type);
    }


    public static TracerProviderBuilder AddTransactionalOutboxInstrumentation(
        this TracerProviderBuilder builder) =>
        builder.AddSource(Diagnostics.ActivitySourceName);
}