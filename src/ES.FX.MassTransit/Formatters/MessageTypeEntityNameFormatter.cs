using ES.FX.Contracts.Messaging;
using MassTransit;
using MassTransit.Internals;

namespace ES.FX.MassTransit.Formatters;

/// <summary>
///     Formatter that uses the <see cref="MessageTypeAttribute" /> to format the entity name. Uses
///     <see cref="IEntityNameFormatter" /> as the base formatter
/// </summary>
/// <param name="entityNameFormatter"><see cref="IEndpointNameFormatter" /> to use as the base formatter</param>
/// <param name="faultFallbackToMessageType">
///     When set to true, the <see cref="MessageTypeAttribute" /> and
///     <param name="faultFormat"></param>
///     will be used to determine the <see cref="Fault" /> message name
/// </param>
/// <param name="faultFormat">
///     The format to use for the fault message type if <see cref="MessageTypeAttribute" /> is set
///     but <see cref="FaultMessageTypeAttribute" /> is not set
/// </param>
public class MessageTypeEntityNameFormatter(
    IEntityNameFormatter entityNameFormatter,
    bool faultFallbackToMessageType = true,
    string faultFormat = "{0}_fault") : IEntityNameFormatter
{
    public string FormatEntityName<TMessage>()
    {
        if (typeof(TMessage).ClosesType(typeof(Fault<>), out Type[] messageTypes))
        {
            var type = FaultMessageTypeAttribute.MessageTypeFor(messageTypes.First());
            if (type is not null) return type;

            if (faultFallbackToMessageType)
            {
                type = MessageTypeAttribute.MessageTypeFor(messageTypes.First());
                if (type is not null) return string.Format(faultFormat, type);
            }
        }
        else
        {
            var type = MessageTypeAttribute.MessageTypeFor(typeof(TMessage));
            if (type is not null) return type;
        }

        return entityNameFormatter.FormatEntityName<TMessage>();
    }
}