using ES.FX.TransactionalOutbox.Delivery;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
    /// <param name="services">The <see cref="IServiceCollection" /> on which to register the required services</param>
    /// <param name="configureOptions">
    ///     The options used to configure the <see cref="OutboxDeliveryService{TDbContext}" />
    /// </param>
    public static IServiceCollection AddOutboxDeliveryService<TDbContext, TMessageHandler>(
        this IServiceCollection services,
        Action<OutboxDeliveryOptions<TDbContext>>? configureOptions = null
    )
        where TDbContext : DbContext
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

    /// <summary>
    ///     Adds the outbox delivery service to the service collection. An implementation of
    ///     <see cref="IOutboxMessageHandler" /> must be registered separately.
    /// </summary>
    /// <typeparam name="TDbContext">The type of <see cref="DbContext" /> for which to process messages</typeparam>
    /// <param name="services">The <see cref="IServiceCollection" /> on which to register the required services</param>
    /// <param name="configureOptions">
    ///     The options used to configure the <see cref="OutboxDeliveryService{TDbContext}" />
    /// </param>
    public static IServiceCollection AddOutboxDeliveryService<TDbContext>(
        this IServiceCollection services,
        Action<OutboxDeliveryOptions<TDbContext>>? configureOptions = null
    )
        where TDbContext : DbContext
    {
        services.AddOptions<OutboxDeliveryOptions<TDbContext>>().Configure(options =>
        {
            configureOptions?.Invoke(options);
        });

        services.AddHostedService<OutboxDeliveryService<TDbContext>>();
        return services;
    }
}