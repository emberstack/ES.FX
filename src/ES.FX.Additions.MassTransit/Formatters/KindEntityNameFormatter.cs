using ES.FX.ComponentModel.DataAnnotations;
using MassTransit;
using MassTransit.Internals;

namespace ES.FX.Additions.MassTransit.Formatters;

/// <summary>
///     Formatter that uses the <see cref="KindAttribute" /> to format the entity name. Uses
///     <see cref="IEntityNameFormatter" /> as the base formatter
/// </summary>
/// <param name="entityNameFormatter"><see cref="IEndpointNameFormatter" /> to use as the base formatter</param>
/// <param name="faultFallbackToKind">
///     When set to true, the <see cref="KindAttribute" /> and
///     <param name="faultFormat"></param>
///     will be used to determine the <see cref="Fault" /> message name
/// </param>
/// <param name="faultFormat">
///     The format to use for the fault type if <see cref="KindAttribute" /> is set
///     but <see cref="FaultKindAttribute" /> is not set
/// </param>
public class KindEntityNameFormatter(
    IEntityNameFormatter entityNameFormatter,
    bool faultFallbackToKind = true,
    string faultFormat = "{0}_fault") : IEntityNameFormatter
{
    /// <summary>
    ///     Formats the entity name for the specified message. Uses the <see cref="KindAttribute" /> to determine the
    ///     name.
    /// </summary>
    /// <typeparam name="TMessage">The message type</typeparam>
    /// <returns>The formatted message name</returns>
    public string FormatEntityName<TMessage>()
    {
        if (typeof(TMessage).ClosesType(typeof(Fault<>), out Type[] types))
        {
            var type = FaultKindAttribute.For(types.First());
            if (type is not null) return type;

            if (!faultFallbackToKind) return entityNameFormatter.FormatEntityName<TMessage>();
            type = KindAttribute.For(types.First());
            if (type is not null) return string.Format(faultFormat, type);
        }
        else
        {
            var type = KindAttribute.For<TMessage>();
            if (type is not null) return type;
        }

        return entityNameFormatter.FormatEntityName<TMessage>();
    }
}