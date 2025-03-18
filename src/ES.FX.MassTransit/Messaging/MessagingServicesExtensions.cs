using ES.FX.Messaging;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.MassTransit.Messaging;

[PublicAPI]
public static class MessagingServicesExtensions
{
    /// <summary>
    /// Registers the <see cref="MassTransitMessenger"/> service implementation of <see cref="IMessenger"/>
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="lifetime">
    ///     The lifetime of the <see cref="MassTransitMessenger" />. Default is
    ///     <see cref="ServiceLifetime.Singleton" />.
    /// </param>
    public static void AddMassTransitMessenger(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        services.Add(new ServiceDescriptor(typeof(IMessenger),typeof(MassTransitMessenger), lifetime));
    }
}