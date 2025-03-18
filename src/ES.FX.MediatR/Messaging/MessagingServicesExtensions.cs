﻿using ES.FX.MediatR.Abstractions.Contracts;
using ES.FX.Messaging;
using JetBrains.Annotations;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.MediatR.Messaging;

[PublicAPI]
public static class MessagingServicesExtensions
{
    /// <summary>
    ///     Registers the <see cref="MessengerRelayMessageHandler" /> service.
    ///     Used to handle <see cref="RelayMessage" /> sending via <see cref="IMessenger" />
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="lifetime">
    ///     The lifetime of the <see cref="MessengerRelayMessageHandler" />. Default is
    ///     <see cref="ServiceLifetime.Singleton" />.
    /// </param>
    public static void AddMessengerHandler(this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        services.Add(new ServiceDescriptor(typeof(IRequestHandler<RelayMessage>), typeof(MessengerRelayMessageHandler),
            lifetime));
    }
}