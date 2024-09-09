using System.Reflection;

namespace ES.FX.TransactionalOutbox.Abstractions.Messages;

/// <summary>
///     Class used to define the message type for registration
/// </summary>
/// <typeparam name="TMessageType">The type of the outbox message payload</typeparam>
public class OutboxMessageType<TMessageType> : IOutboxMessageType where TMessageType : class
{
    public OutboxMessageType()
    {
        MessageType = typeof(TMessageType);
        var typeAttribute =
            typeof(TMessageType).GetCustomAttribute(typeof(OutboxMessageTypeAttribute)) as OutboxMessageTypeAttribute;
        PayloadType = typeAttribute?.MessageType ?? typeof(TMessageType).AssemblyQualifiedName!;
    }

    public string PayloadType { get; init; }
    public Type MessageType { get; init; }
}