namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;

/// <summary>
///     Context for outbox message handler
/// </summary>
/// <param name="MessageType">The received message type</param>
/// <param name="Message">The received message instance</param>
public record OutboxMessageHandlerContext(Type MessageType, object Message);