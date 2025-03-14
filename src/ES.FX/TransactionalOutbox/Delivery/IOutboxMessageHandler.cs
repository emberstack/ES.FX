﻿using JetBrains.Annotations;

namespace ES.FX.TransactionalOutbox.Delivery;

/// <summary>
///     Defines a <see cref="TransactionalOutbox"/> message handler
/// </summary>
[PublicAPI]
public interface IOutboxMessageHandler
{
    /// <summary>
    ///     Handles the outbox message.
    /// </summary>
    /// <param name="context">The message context</param>
    /// <param name="cancellationToken"> Delivery cancellation token</param>
    /// <returns> True if the message was successfully handled, false otherwise (the message delivery will be retried)</returns>
    public ValueTask<bool> HandleAsync(OutboxMessageHandlerContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns whether the handler can deliver messages. This can be used to delay the delivery of messages until certain
    ///     conditions are met (example: health checks)
    /// </summary>
    public ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
}