using ES.FX.MassTransit.Serialization;
using JetBrains.Annotations;
using MassTransit;

namespace ES.FX.MassTransit.Middleware.MessageTypes;

/// <summary>
///     Observer that registers all expected message types with the <see cref="MassTransitMessageTypeProvider" />
/// </summary>
[PublicAPI]
public class ConsumerMessageTypeObserver : IConsumerConfigurationObserver
{
    public void ConsumerConfigured<TConsumer>(IConsumerConfigurator<TConsumer> configurator)
        where TConsumer : class
    {
    }

    public void ConsumerMessageConfigured<TConsumer, TMessage>(
        IConsumerMessageConfigurator<TConsumer, TMessage> configurator)
        where TConsumer : class
        where TMessage : class
    {
        MassTransitMessageTypeProvider.RegisterTypes(typeof(TMessage));
    }
}