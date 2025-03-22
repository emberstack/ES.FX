using ES.FX.Extensions.MassTransit.Serialization;
using MassTransit;

namespace ES.FX.Extensions.MassTransit.Middleware.PayloadTypes;

/// <summary>
///     Observer that registers all expected payload types with the <see cref="MassTransitPayloadTypeProvider" />
/// </summary>
public class PayloadTypeConsumerConfigurationObserver : IConsumerConfigurationObserver
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
        MassTransitPayloadTypeProvider.RegisterTypes(typeof(TMessage));
    }
}