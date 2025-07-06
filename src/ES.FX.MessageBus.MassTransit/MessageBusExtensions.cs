using ES.FX.MessageBus.Infrastructure;
using ES.FX.MessageBus.MassTransit.Internals;
using JetBrains.Annotations;
using MassTransit;

namespace ES.FX.MessageBus.MassTransit;

public static class MessageBusExtensions
{
    [PublicAPI]
    public static void UseMassTransit(this IMessageBusBuilder builder, Action<IBusRegistrationConfigurator>? configure = null)
    {
        builder.Engine = new MassTransitMessageBusEngine(configure);
    }
}