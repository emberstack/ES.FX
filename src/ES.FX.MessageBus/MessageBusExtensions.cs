using ES.FX.MessageBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.MessageBus;

public static class MessageBusExtensions
{
    public static IServiceCollection AddMessageHandler<TMessage, THandler>(this IServiceCollection services)
        where TMessage : class
        where THandler : class, IMessageHandler<TMessage>
    {
        services.AddTransient<MessageHandlerDefinition<TMessage, THandler>>();
        return services;
    }


    public static IServiceCollection AddMessageHandler<THandler>(this IServiceCollection services)
        where THandler : class
    {
        var interfaces = typeof(THandler).GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageHandler<>)).ToList();

        foreach (var handlerInterface in interfaces)
        {
            services.AddTransient(typeof(MessageHandlerDefinition<,>).MakeGenericType(
                handlerInterface.GetGenericArguments().First(),
                typeof(THandler)));
        }

        return services;
    }
}