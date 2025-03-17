using ES.FX.TransactionalOutbox;
using JetBrains.Annotations;

namespace ES.FX.Messaging;

/// <summary>
///     Interface used to define messages.
///     All messages are by default <see cref="IOutboxMessage"/>
/// </summary>
[PublicAPI]
public interface IMessage : IOutboxMessage;