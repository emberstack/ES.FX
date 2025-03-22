using ES.FX.ComponentModel.DataAnnotations;
using MassTransit;
using MassTransit.Internals;

namespace ES.FX.Extensions.MassTransit.Formatters;

/// <summary>
///     Formatter that uses the <see cref="PayloadTypeAttribute" /> to format the entity name. Uses
///     <see cref="IEntityNameFormatter" /> as the base formatter
/// </summary>
/// <param name="entityNameFormatter"><see cref="IEndpointNameFormatter" /> to use as the base formatter</param>
/// <param name="faultFallbackToPayloadType">
///     When set to true, the <see cref="PayloadTypeAttribute" /> and
///     <param name="faultFormat"></param>
///     will be used to determine the <see cref="Fault" /> message name
/// </param>
/// <param name="faultFormat">
///     The format to use for the fault type if <see cref="PayloadTypeAttribute" /> is set
///     but <see cref="FaultPayloadTypeAttribute" /> is not set
/// </param>
public class PayloadTypeEntityNameFormatter(
    IEntityNameFormatter entityNameFormatter,
    bool faultFallbackToPayloadType = true,
    string faultFormat = "{0}_fault") : IEntityNameFormatter
{
    /// <summary>
    ///     Formats the entity name for the specified message. Uses the <see cref="PayloadTypeAttribute" /> to determine the
    ///     name.
    /// </summary>
    /// <typeparam name="TMessage">The message type</typeparam>
    /// <returns>The formatted message name</returns>
    public string FormatEntityName<TMessage>()
    {
        if (typeof(TMessage).ClosesType(typeof(Fault<>), out Type[] payloadTypes))
        {
            var type = FaultPayloadTypeAttribute.PayloadTypeFor(payloadTypes.First());
            if (type is not null) return type;

            if (!faultFallbackToPayloadType) return entityNameFormatter.FormatEntityName<TMessage>();
            type = PayloadTypeAttribute.PayloadTypeFor(payloadTypes.First());
            if (type is not null) return string.Format(faultFormat, type);
        }
        else
        {
            var type = PayloadTypeAttribute.PayloadTypeFor<TMessage>();
            if (type is not null) return type;
        }

        return entityNameFormatter.FormatEntityName<TMessage>();
    }
}