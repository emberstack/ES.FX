namespace ES.FX.TransactionalOutbox.Abstractions.Messages;

/// <summary>
///     Attribute used to specify a custom name for the message type. This is used to override the default message type
///     (assembly qualified name) and is useful when the message type is now longer available or has changed
/// </summary>
/// <remarks>
/// </remarks>
/// <param name="messageType">The name to use for the message type</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public class OutboxMessageTypeAttribute(string messageType) : Attribute
{
    public string MessageType { get; } = messageType;
}