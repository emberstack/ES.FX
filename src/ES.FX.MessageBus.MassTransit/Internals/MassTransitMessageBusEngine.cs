using ES.FX.MessageBus.Infrastructure;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ES.FX.MessageBus.MassTransit.Internals;

internal class MassTransitMessageBusEngine(Action<IBusRegistrationConfigurator>? configure = null) : IMessageBusEngine
{
    public IServiceCollection RegisterServices(IServiceCollection services)
    {
        services.AddMassTransit(x =>
        {
            var handlerDefinitions = x.Where(sd =>
                    sd.ServiceType.IsGenericType
                    && sd.ServiceType.GetGenericTypeDefinition() == typeof(MessageHandlerDefinition<,>))
                .ToList();

            foreach (var definition in handlerDefinitions)
            {
                var messageType = definition.ServiceType.GenericTypeArguments.First();
                var handlerType = definition.ServiceType.GenericTypeArguments.Last();

                // In case the handler is not registered as a service, add it as scoped.
                x.TryAddScoped(handlerType);

                var consumerType = typeof(MassTransitConsumer<,>)
                    .MakeGenericType(messageType, handlerType);
                x.AddConsumer(consumerType);

            }

            configure?.Invoke(x);
        });


        services.AddScoped<IMessageBus, MassTransitMessageBus>();
        services.AddSingleton<IMessageBusControl, MassTransitMessageBusControl>();

        return services;
    }
}