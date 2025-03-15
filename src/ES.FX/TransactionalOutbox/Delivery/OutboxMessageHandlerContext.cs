using JetBrains.Annotations;

namespace ES.FX.TransactionalOutbox.Delivery;

/// <summary>
///     Context for outbox message handler
/// </summary>
/// <param name="MessageType">The received message type</param>
/// <param name="Message">The received message instance</param>
[PublicAPI]
public record OutboxMessageHandlerContext(Type MessageType, object Message);