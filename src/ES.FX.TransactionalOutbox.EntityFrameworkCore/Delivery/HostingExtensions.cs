using ES.FX.TransactionalOutbox.Delivery;
using ES.FX.TransactionalOutbox.Delivery.Faults;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;

[PublicAPI]
public static class HostingExtensions
{
    /// <summary>
    ///     Adds the outbox delivery service to the service collection. The <see cref="TMessageHandler" /> will be used to
    ///     deliver the messages.
    /// </summary>
    /// <typeparam name="TDbContext">The type of <see cref="DbContext" /> for which to process messages</typeparam>
    /// <typeparam name="TMessageHandler"> The type of <see cref="IOutboxMessageHandler" /> used to delivery messages </typeparam>
    /// <typeparam name="TMessageFaultHandler">
    ///     The type of <see cref="IOutboxMessageFaultHandler" /> used to handle delivery
    ///     faults
    /// </typeparam>
    /// <param name="services">The <see cref="IServiceCollection" /> on which to register the required services</param>
    /// <param name="configureOptions">
    ///     The options used to configure the <see cref="OutboxDeliveryService{TDbContext}" />
    /// </param>
    public static IServiceCollection AddOutboxDeliveryService<TDbContext, TMessageHandler, TMessageFaultHandler>(
        this IServiceCollection services,
        Action<OutboxDeliveryOptions<TDbContext>>? configureOptions = null
    )
        where TDbContext : DbContext
        where TMessageHandler : class, IOutboxMessageHandler
        where TMessageFaultHandler : class, IOutboxMessageFaultHandler
    {
        services.AddOutboxMessageHandler<TDbContext, TMessageHandler>();
        services.AddOutboxMessageFaultHandler<TDbContext, TMessageFaultHandler>();
        services.AddOutboxDeliveryService(configureOptions);
        return services;
    }


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
    /// <remarks> This will use the <see cref="DefaultOutboxMessageFaultHandler" /> to handle delivery faults.</remarks>
    public static IServiceCollection AddOutboxDeliveryService<TDbContext, TMessageHandler>(
        this IServiceCollection services,
        Action<OutboxDeliveryOptions<TDbContext>>? configureOptions = null
    )
        where TDbContext : DbContext
        where TMessageHandler : class, IOutboxMessageHandler =>
        services.AddOutboxDeliveryService<TDbContext, TMessageHandler, DefaultOutboxMessageFaultHandler>(
            configureOptions);


    /// <summary>
    ///     Adds the outbox delivery service to the service collection.
    /// </summary>
    /// <typeparam name="TDbContext">The type of <see cref="DbContext" /> for which to process messages</typeparam>
    /// <param name="services">The <see cref="IServiceCollection" /> on which to register the required services</param>
    /// <param name="configureOptions">
    ///     The options used to configure the <see cref="OutboxDeliveryService{TDbContext}" />
    /// </param>
    /// <remarks>
    ///     <see cref="IOutboxMessageHandler" /> must be registered in the service collection as a keyed service.
    ///     <see cref="IOutboxMessageFaultHandler" /> must be registered in the service collection as a keyed service.
    /// </remarks>
    public static IServiceCollection AddOutboxDeliveryService<TDbContext>(
        this IServiceCollection services,
        Action<OutboxDeliveryOptions<TDbContext>>? configureOptions = null
    )
        where TDbContext : DbContext
    {
        services.TryAddKeyedScoped<IOutboxMessageFaultHandler, DefaultOutboxMessageFaultHandler>(typeof(TDbContext));

        services.AddOptions<OutboxDeliveryOptions<TDbContext>>().Configure(options =>
        {
            configureOptions?.Invoke(options);
        });

        services.AddHostedService<OutboxDeliveryService<TDbContext>>();

        return services;
    }


    /// <summary>
    ///     / Adds the <see cref="IOutboxMessageHandler" /> to the service collection as a keyed service for the specified
    ///     <see cref="DbContext" /> type.
    /// </summary>
    /// <typeparam name="TDbContext"> The type of <see cref="DbContext" /> for which to process messages</typeparam>
    /// <typeparam name="TMessageHandler"> The type of <see cref="IOutboxMessageHandler" /> used to delivery messages </typeparam>
    /// <param name="services"> The <see cref="IServiceCollection" /> on which to register the required services</param>
    public static IServiceCollection AddOutboxMessageHandler<TDbContext, TMessageHandler>(
        this IServiceCollection services)
        where TDbContext : DbContext
        where TMessageHandler : class, IOutboxMessageHandler
    {
        services.AddKeyedScoped<IOutboxMessageHandler, TMessageHandler>(typeof(TDbContext));
        return services;
    }

    /// <summary>
    ///     / Adds the <see cref="IOutboxMessageFaultHandler" /> to the service collection as a keyed service for the specified
    ///     <see cref="DbContext" /> type.
    /// </summary>
    /// <typeparam name="TDbContext"> The type of <see cref="DbContext" /> for which to process messages</typeparam>
    /// <typeparam name="TMessageFaultHandler">
    ///     The type of <see cref="IOutboxMessageFaultHandler" /> used to handle delivery
    ///     faults
    /// </typeparam>
    /// <param name="services"> The <see cref="IServiceCollection" /> on which to register the required services</param>
    public static IServiceCollection AddOutboxMessageFaultHandler<TDbContext, TMessageFaultHandler>(
        this IServiceCollection services)
        where TDbContext : DbContext
        where TMessageFaultHandler : class, IOutboxMessageFaultHandler
    {
        services.AddKeyedScoped<IOutboxMessageFaultHandler, TMessageFaultHandler>(typeof(TDbContext));
        return services;
    }
}