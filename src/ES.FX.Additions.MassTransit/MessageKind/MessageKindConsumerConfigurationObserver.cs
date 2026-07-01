using MassTransit;

namespace ES.FX.Additions.MassTransit.MessageKind;

/// <summary>
///     Observer that registers all expected message types with the <see cref="MessageKindProvider" />
/// </summary>
public class MessageKindConsumerConfigurationObserver : IConsumerConfigurationObserver
{
    /// <summary>
    ///     Called when a consumer is configured. No registration is required at the consumer level.
    /// </summary>
    /// <typeparam name="TConsumer">The consumer type</typeparam>
    /// <param name="configurator">The consumer configurator</param>
    public void ConsumerConfigured<TConsumer>(IConsumerConfigurator<TConsumer> configurator)
        where TConsumer : class
    {
    }

    /// <summary>
    ///     Called when a consumer message is configured. Registers the message type with the
    ///     <see cref="MessageKindProvider" />.
    /// </summary>
    /// <typeparam name="TConsumer">The consumer type</typeparam>
    /// <typeparam name="TMessage">The message type</typeparam>
    /// <param name="configurator">The consumer message configurator</param>
    public void ConsumerMessageConfigured<TConsumer, TMessage>(
        IConsumerMessageConfigurator<TConsumer, TMessage> configurator)
        where TConsumer : class
        where TMessage : class
    {
        MessageKindProvider.RegisterTypes(typeof(TMessage));
    }
}