using JetBrains.Annotations;
using MassTransit;

namespace ES.FX.Additions.MassTransit.MessageKind;

[PublicAPI]
public static class MessageKindExtensions
{
    public static void UseMessageKind(this IBusFactoryConfigurator cfg, IRegistrationContext registrationContext)
    {
        //Monitor consumers to see what message types are needed
        cfg.ConnectConsumerConfigurationObserver(new MessageKindConsumerConfigurationObserver());

        //Add a filter to annotate the outgoing messages with the message type
        cfg.UsePublishFilter(typeof(MessageKindPublishFilter<>), registrationContext);
    }
}