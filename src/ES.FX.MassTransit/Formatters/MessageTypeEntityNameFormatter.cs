using ES.FX.Contracts.Messaging;
using MassTransit;

namespace ES.FX.MassTransit.Formatters;

/// <summary>
///     Formatter that uses the <see cref="MessageTypeAttribute" /> to format the entity name. Uses
///     <see cref="IEntityNameFormatter" /> as the base formatter
/// </summary>
/// <param name="entityNameFormatter"><see cref="IEndpointNameFormatter" /> to use as the base formatter</param>
public class MessageTypeEntityNameFormatter(IEntityNameFormatter entityNameFormatter) : IEntityNameFormatter
{
    public string FormatEntityName<TMessage>() =>
        MessageTypeAttribute.TypeFor(typeof(TMessage)) ??
        entityNameFormatter.FormatEntityName<TMessage>();
}