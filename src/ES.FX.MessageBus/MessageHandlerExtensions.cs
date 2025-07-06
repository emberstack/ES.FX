using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ES.FX.MessageBus;

[PublicAPI]
public static class MessageHandlerExtensions
{
    public static IServiceCollection AddMessageHandler(this IServiceCollection services, Type messageType, Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        if (!messageType.IsClass)
            throw new ArgumentException("Message type must be a class", nameof(messageType));

        ArgumentNullException.ThrowIfNull(handlerType);
        if (!handlerType.IsClass || handlerType.IsAbstract)
            throw new ArgumentException("Message handler type must be a non-abstract class", nameof(handlerType));


        var handlerInterface = typeof(IMessageHandler<>).MakeGenericType(messageType);

        if (!handlerInterface.IsAssignableFrom(handlerType))
            throw new ArgumentException(
                $"Message handler type must implement {typeof(IMessageHandler<>).MakeGenericType(messageType).Name}",
                nameof(handlerType));


        var handlerDefinition = typeof(MessageHandlerDefinition<,>)
            .MakeGenericType(messageType, handlerType);

        services.RemoveAll(handlerDefinition);
        services.AddSingleton(handlerDefinition);
        return services;

    }


    public static IServiceCollection AddMessageHandler<TMessage, THandler>(this IServiceCollection services)
        where TMessage : class
        where THandler : class, IMessageHandler =>
        services.AddMessageHandler(typeof(TMessage), typeof(THandler));


    public static IServiceCollection AddMessageHandler<THandler>(this IServiceCollection services)
        where THandler : class, IMessageHandler =>
        services.AddMessageHandler(typeof(THandler));

    public static IServiceCollection AddMessageHandler(this IServiceCollection services, Type handlerType)
    {
        var handlerInterfaces = handlerType.GetInterfaces()
            .Where(i =>
                i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(IMessageHandler<>)
            ).ToList();

        foreach (var handlerInterface in handlerInterfaces)
        {
            services.AddMessageHandler(handlerInterface.GetGenericArguments().First(), handlerType);
        }

        return services;
    }


    public static IServiceCollection AddMessageHandlers(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies == null || assemblies.Length == 0)
            throw new ArgumentException("At least one assembly must be provided.", nameof(assemblies));

        var types = assemblies
            .SelectMany(a => a.DefinedTypes)
            .Where(t =>
                t is { IsClass: true, IsAbstract: false }
                && t.ImplementedInterfaces.Any(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IMessageHandler<>)
                )
            )
            .Select(t => t.AsType())
            .Distinct().ToArray();

        foreach (var type in types)
        {
            services.AddMessageHandler(type);
        }

        return services;
    }
}