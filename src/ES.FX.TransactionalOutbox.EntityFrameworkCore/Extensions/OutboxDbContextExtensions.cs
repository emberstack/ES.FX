using System.Diagnostics;
using ES.FX.TransactionalOutbox.Delivery;
using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.EntityTypeConfigurations;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Internals;
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
    public static void UseOutbox(this DbContextOptionsBuilder optionsBuilder,
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
    public static void AddOutboxEntities(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Outbox>().ToTable("__Outboxes");
        modelBuilder.ApplyConfiguration(new OutboxEntityTypeConfiguration());

        modelBuilder.Entity<OutboxMessage>().ToTable("__OutboxMessages");
        modelBuilder.ApplyConfiguration(new OutboxMessageEntityTypeConfiguration());
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