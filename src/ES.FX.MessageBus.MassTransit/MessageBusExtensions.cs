using ES.FX.MessageBus.Abstractions;
using ES.FX.MessageBus.MassTransit.Internals;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ES.FX.MessageBus.MassTransit;

public static class MessageBusExtensions
{
    public static void AddMessageBus(this IServiceCollection services, Action<IBusRegistrationConfigurator> configure)
    {
        services.AddMassTransit(x=>
        {
            //var handlerServiceDescriptors = x.Where(sd =>
            //        sd.ServiceType.IsGenericType
            //        && sd.ServiceType.GetGenericTypeDefinition() == typeof(IHandleMessages<>))
            //    .ToList();

            //foreach (var handlerServiceDescriptor in handlerServiceDescriptors)
            //{
            //    var messageType = handlerServiceDescriptor.ServiceType.GenericTypeArguments.First();
            //    var handlerType = handlerServiceDescriptor.ImplementationType!;

            //    x.TryAddScoped(handlerType);

            //    var consumerType = typeof(MassTransitConsumer<,>)
            //        .MakeGenericType(messageType, handlerType);
            //    x.AddConsumer(consumerType);
            //}

            var handlerDefinitions = x.Where(sd =>
                        sd.ServiceType.IsGenericType
                        && sd.ServiceType.GetGenericTypeDefinition() == typeof(MessageHandlerDefinition<,>))
                    .ToList();

            foreach (var definition in handlerDefinitions)
            {
                var messageType = definition.ServiceType.GenericTypeArguments.First();
                var handlerType = definition.ServiceType.GenericTypeArguments.Last()!;

                x.TryAddScoped(handlerType);

                var consumerType = typeof(MassTransitConsumer<,>)
                    .MakeGenericType(messageType, handlerType);
                x.AddConsumer(consumerType);
            }

            configure(x);
        });
        services.AddScoped<IMessageBus, MassTransitMessageBus>();
        services.AddSingleton<IMessageBusControl, MassTransitMessageBusControl>();
    }

   
}