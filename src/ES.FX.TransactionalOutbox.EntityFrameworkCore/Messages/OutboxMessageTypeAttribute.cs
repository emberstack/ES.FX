namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Messages;

/// <summary>
///     Attribute used to specify a custom name for the message type. This is used to override the default message type
///     (assembly qualified name) and is useful when the message type is now longer available or has changed
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public class OutboxMessageTypeAttribute : Attribute
{
    /// <summary>
    /// </summary>
    /// <param name="messageType">The name to use for the message type</param>
    public OutboxMessageTypeAttribute(string messageType) => MessageType = messageType;

    public string MessageType { get; }
}