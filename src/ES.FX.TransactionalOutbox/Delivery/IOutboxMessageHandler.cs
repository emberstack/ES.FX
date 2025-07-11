﻿using JetBrains.Annotations;

namespace ES.FX.TransactionalOutbox.Delivery;

/// <summary>
///     Interface used to define a <see cref="IOutboxMessageHandler" /> that handles <see cref="OutboxMessageContext" />
/// </summary>
[PublicAPI]
public interface IOutboxMessageHandler
{
    /// <summary>
    ///     Handles the outbox message.
    /// </summary>
    /// <param name="cancellationToken"> Delivery cancellation token</param>
    public ValueTask Handle(OutboxMessageContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns whether the handler can deliver messages. This can be used to delay the delivery of messages until certain
    ///     conditions are met (example: health checks)
    /// </summary>
    public ValueTask<bool> IsReady(CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
}