using System.Diagnostics;
using ES.FX.TransactionalOutbox.Delivery;
using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.EntityTypeConfigurations;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Internals;
using ES.FX.TransactionalOutbox.Interceptors;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Extensions;

[PublicAPI]
public static class OutboxDbContextExtensions
{
    /// <summary>
    ///     Configures the <see cref="DbContext" /> to support the outbox pattern.
    /// </summary>
    /// <param name="optionsBuilder"></param>
    public static void AddOutbox(this DbContextOptionsBuilder optionsBuilder,
        Action<OutboxDbContextOptions>? configureOptions = null)
    {
        OutboxDbContextOptions outboxDbContextOptions;
        var outboxDbContextOptionsExtension = optionsBuilder.Options.FindExtension<OutboxDbContextOptionsExtension>();
        if (outboxDbContextOptionsExtension is not null)
        {
            outboxDbContextOptions = outboxDbContextOptionsExtension.OutboxDbContextOptions;
        }
        else
        {
            outboxDbContextOptions = new OutboxDbContextOptions();
            outboxDbContextOptionsExtension = new OutboxDbContextOptionsExtension(outboxDbContextOptions);
            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder)
                .AddOrUpdateExtension(outboxDbContextOptionsExtension);
        }

        configureOptions?.Invoke(outboxDbContextOptions);
    }


    /// <summary>
    ///     Adds the required Outbox entities to the model builder
    /// </summary>
    /// <param name="modelBuilder">The <see cref="ModelBuilder" /> to configure </param>
    public static void AddOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Outbox>().ToTable("__Outboxes");
        modelBuilder.ApplyConfiguration(new OutboxEntityTypeConfiguration());

        modelBuilder.Entity<OutboxMessage>().ToTable("__OutboxMessages");
        modelBuilder.ApplyConfiguration(new OutboxMessageEntityTypeConfiguration());

        modelBuilder.Entity<OutboxMessageFault>().ToTable("__OutboxMessageFaults");
        modelBuilder.ApplyConfiguration(new OutboxMessageFaultEntityTypeConfiguration());
    }


    /// <summary>
    ///     Adds a message to the outbox. Use the <see cref="OutboxMessageDeliveryOptions" /> to configure the delivery
    ///     options.
    /// </summary>
    /// <typeparam name="TOutboxDbContext">The <see cref="TOutboxDbContext" /> used to add the message</typeparam>
    /// <typeparam name="TMessage"> <see cref="TMessage" /> type of message payload</typeparam>
    /// <param name="dbContext"> <see cref="TOutboxDbContext" /> instance to add the message to</param>
    /// <param name="message">The message payload</param>
    /// <param name="deliveryOptions">The options used to configure the message delivery</param>
    public static void AddOutboxMessage<TOutboxDbContext, TMessage>(this TOutboxDbContext dbContext, TMessage message,
        OutboxMessageDeliveryOptions? deliveryOptions = null)
        where TOutboxDbContext : DbContext
        where TMessage : class
    {
        deliveryOptions ??= new OutboxMessageDeliveryOptions();

        var outboxDbContextOptions = dbContext.GetService<OutboxDbContextOptions>();

        var messageContext = new OutboxMessageInterceptorContext
        {
            AddedAt = DateTimeOffset.UtcNow,
            PayloadType = typeof(TMessage),
            Payload = message,
            Headers = new Dictionary<string, string>(),
            ActivityId = Activity.Current?.Id,
            DeliveryOptions = deliveryOptions
        };

        foreach (var messageInterceptor in outboxDbContextOptions.MessageInterceptors)
            messageInterceptor.Intercept(messageContext);

        dbContext.Set<OutboxMessage>().Add(new OutboxMessage
        {
            AddedAt = messageContext.AddedAt,
            Headers = outboxDbContextOptions.Serializer.SerializeHeaders(messageContext.Headers,
                messageContext.PayloadType),
            Payload = outboxDbContextOptions.Serializer.SerializePayload(messageContext.Payload,
                messageContext.PayloadType, out var payloadType),
            PayloadType = payloadType,
            ActivityId = messageContext.ActivityId,
            DeliveryAttempts = 0,
            DeliveryFirstAttemptedAt = null,
            DeliveryLastAttemptError = null,
            DeliveryLastAttemptedAt = null,
            DeliveryNotBefore = messageContext.DeliveryOptions.NotBefore,
            DeliveryNotAfter = messageContext.DeliveryOptions.NotAfter,
            DeliveryMaxAttempts = messageContext.DeliveryOptions.MaxAttempts,
            DeliveryAttemptDelay = messageContext.DeliveryOptions.DelayBetweenAttempts,
            DeliveryAttemptDelayIsExponential = messageContext.DeliveryOptions.DelayBetweenAttemptsIsExponential
        });
    }
}