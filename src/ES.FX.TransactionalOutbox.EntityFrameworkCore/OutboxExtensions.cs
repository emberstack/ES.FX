using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using ES.FX.Contracts.TransactionalOutbox;
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
    public static void AddOutboxBehavior(this DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(new OutboxDbContextInterceptor());


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
        where TMessage : class, IOutboxMessage
    {
        deliveryOptions ??= new OutboxMessageOptions();
        var headers = new Dictionary<string, string?>
        {
            { OutboxMessageHeaders.Diagnostics.ActivityId, Activity.Current?.Id },
            { OutboxMessageHeaders.Message.PayloadOriginalType, typeof(TMessage).AssemblyQualifiedName }
        };

        dbContext.Set<OutboxMessage>().Add(new OutboxMessage
        {
            AddedAt = DateTimeOffset.UtcNow,
            PayloadType = OutboxPayloadTypeProvider.GetPayloadType(message.GetType()),
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
    public static void AddMessageType<TDbContext, TMessageType>(this OutboxDeliveryOptions<TDbContext> options)
        where TDbContext : DbContext, IOutboxDbContext
        where TMessageType : class, IOutboxMessage
    {
        options.MessageTypes.Add(typeof(TMessageType));
    }

    /// <summary>
    ///     Registers known message types. This is used to determine the <see cref="Type" /> of the payload.
    /// </summary>
    public static void AddMessageType<TDbContext>(this OutboxDeliveryOptions<TDbContext> options,
        params Type[] messageTypes) where TDbContext : DbContext, IOutboxDbContext
    {
        foreach (var messageType in messageTypes) options.AddMessageType(messageType);
    }


    /// <summary>
    ///     Registers known message types. This is used to determine the <see cref="Type" /> of the payload.
    /// </summary>
    public static void AddMessageTypes<TDbContext>(this OutboxDeliveryOptions<TDbContext> options,
        Func<Type, bool> filter,
        params Assembly[] assemblies) where TDbContext : DbContext, IOutboxDbContext
    {
        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes().Where(t =>
                t.IsAssignableTo(typeof(IOutboxMessage)) &&
                t is { IsClass: true, IsAbstract: false }).ToArray();
            foreach (var type in types)
                if (filter?.Invoke(type) ?? true)
                    options.AddMessageType(type);
        }
    }

    /// <summary>
    ///     Registers known message types. This is used to determine the <see cref="Type" /> of the payload.
    /// </summary>
    public static void AddMessageTypes<TDbContext>(this OutboxDeliveryOptions<TDbContext> options,
        params Assembly[] assemblies) where TDbContext : DbContext, IOutboxDbContext
    {
        options.AddMessageTypes(_ => true, assemblies);
    }


    /// <summary>
    ///     Registers known message types. This is used to determine the <see cref="Type" /> of the payload.
    /// </summary>
    public static void AddMessageTypesFromAssemblyContaining<TDbContext>(
        this OutboxDeliveryOptions<TDbContext> options, Type type, Func<Type, bool>? filter = null)
        where TDbContext : DbContext, IOutboxDbContext
    {
        options.AddMessageTypes(filter ?? (_ => true), type.Assembly);
    }


    /// <summary>
    ///     Registers known message types. This is used to determine the <see cref="Type" /> of the payload.
    /// </summary>
    public static void AddMessageTypesFromAssemblyContaining<TDbContext, TType>(
        this OutboxDeliveryOptions<TDbContext> options, Func<Type, bool>? filter = null)
        where TDbContext : DbContext, IOutboxDbContext
    {
        options.AddMessageTypes(filter ?? (_ => true), typeof(Type).Assembly);
    }

    /// <summary>
    ///     Registers a message type as a known type. This is used to determine the <see cref="Type" /> of the payload.
    /// </summary>
    public static void AddMessageType<TDbContext>(this OutboxDeliveryOptions<TDbContext> options,
        Type messageType) where TDbContext : DbContext, IOutboxDbContext
    {
        if (!messageType.IsClass || messageType.IsAbstract)
            throw new ArgumentException($"Cannot use {messageType}. Messages must be non-abstract classes.",
                nameof(messageType));

        if (!messageType.IsAssignableTo(typeof(IOutboxMessage)))
            throw new ArgumentException($"Cannot use {messageType}. Messages must implement {nameof(IOutboxMessage)}",
                nameof(messageType));

        options.MessageTypes.Add(messageType);
    }


    public static TracerProviderBuilder AddOutboxInstrumentation(
        this TracerProviderBuilder builder) =>
        builder.AddSource(Diagnostics.ActivitySourceName);


    /// <summary>
    ///     Adds the outbox delivery service to the service collection. The <see cref="TMessageHandler" /> will be used to
    ///     deliver the messages.
    /// </summary>
    /// <typeparam name="TDbContext">The type of <see cref="DbContext" /> for which to process messages</typeparam>
    /// <typeparam name="TMessageHandler"> The type of <see cref="IOutboxMessageHandler" /> used to delivery messages </typeparam>
    /// <param name="services">The <see cref="IServiceCollection" /> on which to register the required services</param>
    /// <param name="configureOptions">
    ///     The options used to configure the <see cref="OutboxDeliveryService{TDbContext}" />
    /// </param>
    public static IServiceCollection AddOutboxDeliveryService<TDbContext, TMessageHandler>(
        this IServiceCollection services,
        Action<OutboxDeliveryOptions<TDbContext>>? configureOptions = null
    )
        where TDbContext : DbContext, IOutboxDbContext
        where TMessageHandler : class, IOutboxMessageHandler
    {
        services.AddOptions<OutboxDeliveryOptions<TDbContext>>().Configure(options =>
        {
            configureOptions?.Invoke(options);
        });

        services.AddHostedService<OutboxDeliveryService<TDbContext>>();
        services.AddScoped<IOutboxMessageHandler, TMessageHandler>();
        return services;
    }
}