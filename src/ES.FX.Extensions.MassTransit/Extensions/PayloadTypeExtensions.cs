using ES.FX.Extensions.MassTransit.Middleware.PayloadTypes;
using JetBrains.Annotations;
using MassTransit;

namespace ES.FX.Extensions.MassTransit.Extensions;

[PublicAPI]
public static class PayloadTypeExtensions
{
    public static void UsePayloadType(this IBusFactoryConfigurator cfg, IRegistrationContext registrationContext)
    {
        //Monitor consumers to see what message types are needed
        cfg.ConnectConsumerConfigurationObserver(new PayloadTypeConsumerConfigurationObserver());

        //Add a filter to annotate the outgoing messages with the message type
        cfg.UsePublishFilter(typeof(PayloadTypeHeaderPublishFilter<>), registrationContext);
    }
}