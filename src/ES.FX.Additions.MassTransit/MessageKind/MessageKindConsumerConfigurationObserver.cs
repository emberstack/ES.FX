using MassTransit;

namespace ES.FX.Additions.MassTransit.MessageKind;

/// <summary>
///     Observer that registers all expected message types with the <see cref="MessageKindProvider" />
/// </summary>
public class MessageKindConsumerConfigurationObserver : IConsumerConfigurationObserver
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
        MessageKindProvider.RegisterTypes(typeof(TMessage));
    }
}