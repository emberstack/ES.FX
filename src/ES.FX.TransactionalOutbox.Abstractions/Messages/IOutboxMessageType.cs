namespace ES.FX.TransactionalOutbox.Abstractions.Messages;

/// <summary>
///     Interface used to define the message type for registration
/// </summary>
public interface IOutboxMessageType
{
    public string PayloadType { get; }
    public Type MessageType { get; }
}